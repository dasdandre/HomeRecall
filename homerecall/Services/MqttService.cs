using System.Text;
using System.Text.Json;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using MQTTnet.Packets;

namespace HomeRecall.Services;

public class MqttService : IMqttService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _protector;
    private readonly ILogger<MqttService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private IManagedMqttClient? _mqttClient;    
    private string[] _excludedDeviceTypes = Array.Empty<string>();
    
    private MqttConnectionStatus _status = MqttConnectionStatus.Disconnected;
    public MqttConnectionStatus Status 
    { 
        get => _status;
        private set 
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke();
            }
        }
    }

    public string? LastErrorMessage { get; private set; }
    public event Action? StatusChanged;

    public MqttService(IServiceScopeFactory scopeFactory, IDataProtectionProvider protectionProvider, ILogger<MqttService> logger, IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _protector = protectionProvider.CreateProtector("MqttPassword");
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        
        // Start connection on a background task to not block initial registration
        Task.Run(() => ReconnectAsync());
    }

    public async Task ReconnectAsync()
    {
        try 
        {
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BackupContext>();
            var settings = await context.Settings.FirstOrDefaultAsync();

            if (settings == null || !settings.MqttEnabled || string.IsNullOrEmpty(settings.MqttHost))
            {
                Status = MqttConnectionStatus.Disabled;
                return;
            }

            Status = MqttConnectionStatus.Connecting;
            LastErrorMessage = null;

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateManagedMqttClient();

            var password = string.Empty;
            if (!string.IsNullOrEmpty(settings.MqttPasswordEncrypted))
            {
                try
                {
                    password = _protector.Unprotect(settings.MqttPasswordEncrypted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt MQTT password");
                }
            }

            var messageBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"HomeRecall-{Guid.NewGuid().ToString("N").Substring(0, 6)}")
                .WithCleanSession();

            if (settings.MqttHost.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || 
                settings.MqttHost.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                messageBuilder.WithWebSocketServer(o => o.WithUri(settings.MqttHost));
            }
            else
            {
                messageBuilder.WithTcpServer(settings.MqttHost, settings.MqttPort);
            }

            if (!string.IsNullOrEmpty(settings.MqttUsername))
            {
                messageBuilder.WithCredentials(settings.MqttUsername, password);
            }

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(messageBuilder.Build())
                .Build();

            _mqttClient.ConnectedAsync += e => {
                Status = MqttConnectionStatus.Connected;
                LastErrorMessage = null;
                _logger.LogInformation("Connected to MQTT Broker");
                return Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += e => {
                string reason = e.Reason.ToString();
                if (e.Exception != null)
                {
                    LastErrorMessage = e.Exception.Message;
                    Status = MqttConnectionStatus.Error;
                    _logger.LogWarning("Disconnected from MQTT Broker: {Reason}. Exception: {Message}", reason, e.Exception.Message);
                }
                else
                {
                    Status = MqttConnectionStatus.Disconnected;
                    _logger.LogInformation("Disconnected from MQTT Broker: {Reason}", reason);
                }
                return Task.CompletedTask;
            };

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            // Collect discovery topics from non-excluded strategies
            _excludedDeviceTypes = settings.MqttExcludedDeviceTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var strategies = scope.ServiceProvider.GetServices<IDeviceStrategy>();
            
            var topics = strategies
                .Where(s => !_excludedDeviceTypes.Contains(s.SupportedType.ToString()))
                .SelectMany(s => s.MqttDiscoveryTopics)
                .Distinct()
                .Select(t => new MqttTopicFilter { Topic = t })
                .ToList();

            if (topics.Any())
            {
                await _mqttClient.SubscribeAsync(topics);
            }

            await _mqttClient.StartAsync(options);
        }
        catch (Exception ex)
        {
            Status = MqttConnectionStatus.Error;
            LastErrorMessage = ex.Message;
            _logger.LogError(ex, "Error starting MQTT client");
        }
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            using var scope = _scopeFactory.CreateScope();
            var strategies = scope.ServiceProvider.GetServices<IDeviceStrategy>();
            
            foreach (var strategy in strategies)
            {
                // Skip excluded device types
                if (_excludedDeviceTypes.Contains(strategy.SupportedType.ToString()))
                    continue;

                // Only check strategies that have matching topics
                if (!strategy.MqttDiscoveryTopics.Any(pattern => MqttTopicMatches(topic, pattern)))
                    continue;

                var discovered = strategy.DiscoverFromMqtt(topic, payload);
                
                // discovery always runs via mac address
                if (discovered != null && !string.IsNullOrEmpty(discovered.MacAddress))
                {
                    // normalize mac address, don't trust the format from the stragety
                    if (!string.IsNullOrEmpty(discovered.MacAddress))
                    {
                        discovered.MacAddress = NetworkUtils.NormalizeMac(discovered.MacAddress);    
                    }

                    await HandleDiscoveredDevice(discovered);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message");
        }
    }

    private static bool MqttTopicMatches(string topic, string pattern)
    {
        // Handle exact match
        if (topic == pattern)
            return true;

        var topicParts = topic.Split('/');
        var patternParts = pattern.Split('/');

        // Multi-level wildcard # must be last and matches everything after
        if (patternParts.Length > 0 && patternParts[^1] == "#")
        {
            if (topicParts.Length < patternParts.Length - 1)
                return false;

            for (int i = 0; i < patternParts.Length - 1; i++)
            {
                if (patternParts[i] != "+" && patternParts[i] != topicParts[i])
                    return false;
            }
            return true;
        }

        // Must have same number of levels if no multi-level wildcard
        if (topicParts.Length != patternParts.Length)
            return false;

        // Check each level, + matches any single level
        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i] != "+" && patternParts[i] != topicParts[i])
                return false;
        }

        return true;
    }

    private async Task HandleDiscoveredDevice(DiscoveredDevice discovered)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BackupContext>();
            var strategies = scope.ServiceProvider.GetServices<IDeviceStrategy>();
            var settings = await context.Settings.FirstOrDefaultAsync();
            var client = _httpClientFactory.CreateClient();

            if (settings?.MqttAutoAdd == true)
            {
                // does a device with this mac address already exist?
                var foundDevice = await context.Devices.FirstOrDefaultAsync(d => d.MacAddress == discovered.MacAddress );
                if (foundDevice == null)
                {
                    var strategy = strategies.FirstOrDefault(s => s.SupportedType == discovered.Type);
                    if (strategy == null)
                    {
                        _logger.LogWarning("No strategy found for device type {Type}", discovered.Type);
                        return;
                    }   

                    // probe the device to get all information
                    var probedDevice = await strategy.ProbeAsync(discovered.IpAddress, client);     
                    if (probedDevice == null)
                    {
                        _logger.LogWarning("Failed to probe device {IpAddress}", discovered.IpAddress);
                        return;
                    }

                    // create new device
                    var device = new Device
                    {
                        Name = probedDevice.Name,
                        IpAddress = probedDevice.IpAddress,
                        Type = probedDevice.Type,
                        MacAddress = probedDevice.MacAddress,
                        HardwareModel = probedDevice.HardwareModel,
                        CurrentFirmwareVersion = probedDevice.FirmwareVersion
                    };
                    
                    // add device to context
                    context.Devices.Add(device);
                    // save changes
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Auto-added device via MQTT: {Name} {Type} ({IpAddress})", device.Name, device.Type, device.IpAddress);
                }
                else{
                    // same mac, same type, different ip address        
                    if (foundDevice.Type == discovered.Type && foundDevice.IpAddress != discovered.IpAddress)
                    {
                        var strategy = strategies.FirstOrDefault(s => s.SupportedType == discovered.Type);
                        if (strategy == null)
                        {
                            _logger.LogWarning("No strategy found for device type {Type}", discovered.Type);
                            return;
                        }   

                        // probe the device to get all information
                        var probedDevice =  await strategy.ProbeAsync(discovered.IpAddress, client);     
                        if (probedDevice == null)
                        {
                            _logger.LogWarning("Failed to probe device {IpAddress}", discovered.IpAddress);
                            return;
                        }
                    
                        // update device ip address
                        if (probedDevice.Name == foundDevice.Name)
                        {                            
                            foundDevice.IpAddress = discovered.IpAddress;
                            await context.SaveChangesAsync();
                            _logger.LogInformation("Updated device IP address via MQTT: {Name} ({IpAddress})", foundDevice.Name, foundDevice.IpAddress);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding discovered device");
        }
    }

    public void Dispose()
    {
        _mqttClient?.Dispose();
    }
}
