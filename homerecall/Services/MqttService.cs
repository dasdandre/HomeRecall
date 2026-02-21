using System.Text;
using System.Text.Json;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Services.Strategies;
using HomeRecall.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;

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

    private readonly IEnumerable<IDeviceStrategy> _strategies;
    private readonly IEnumerable<IMqttDeviceStrategy> _mqttStrategies;

    public MqttService(IServiceScopeFactory scopeFactory, IDataProtectionProvider protectionProvider, ILogger<MqttService> logger, IHttpClientFactory httpClientFactory, IEnumerable<IDeviceStrategy> strategies)
    {
        _scopeFactory = scopeFactory;
        _protector = protectionProvider.CreateProtector("MqttPassword");
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _strategies = strategies;
        _mqttStrategies = strategies.OfType<IMqttDeviceStrategy>();

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

            // Pre-load known devices to cache
            _knownDevices.Clear();
            var interfaces = await context.NetworkInterfaces.Select(i => new { i.MacAddress, i.IpAddress }).ToListAsync();
            foreach (var i in interfaces)
            {
                if (!string.IsNullOrEmpty(i.MacAddress) && !string.IsNullOrEmpty(i.IpAddress))
                {
                    _knownDevices[i.MacAddress] = i.IpAddress;
                }
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

            _mqttClient.ConnectedAsync += e =>
            {
                Status = MqttConnectionStatus.Connected;
                LastErrorMessage = null;
                _logger.LogInformation("Connected to MQTT Broker");
                return Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += e =>
            {
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

            var topics = _mqttStrategies
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

            // Trigger initial discovery and start loop
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Wait a bit for connection to stabilize
                await PublishDiscoveryMessages();
                await StartDiscoveryLoop();
            });
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

            foreach (var strategy in _mqttStrategies)
            {
                // Skip excluded device types
                if (_excludedDeviceTypes.Contains(strategy.SupportedType.ToString()))
                    continue;

                // Only check strategies that have matching topics
                if (!strategy.MqttDiscoveryTopics.Any(pattern => MqttTopicMatches(topic, pattern)))
                    continue;

                var discovered = strategy.DiscoverFromMqtt(topic, payload);

                // discovery always runs via mac address
                if (discovered != null && discovered.Interfaces.Any(i => !string.IsNullOrEmpty(i.MacAddress)))
                {
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

    // Cache of known devices: MacAddress -> IpAddress
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _knownDevices = new();

    // Set of devices currently being processed (MacAddress)
    // Used to drop duplicate discovery messages while one is already being handled.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _processingDevices = new();

    private async Task HandleDiscoveredDevice(DiscoveredDevice discovered)
    {
        var primaryInterface = discovered.Interfaces.FirstOrDefault(i => !string.IsNullOrEmpty(i.MacAddress));
        if (primaryInterface == null || primaryInterface.MacAddress == null) return;
        var macAddress = primaryInterface.MacAddress;
        var ipAddress = primaryInterface.IpAddress;

        // Optimization: Check cache first to avoid creating a scope and hitting the DB
        // If we know this MAC, and the IP is the same, we can skip DB operations entirely.
        if (_knownDevices.TryGetValue(macAddress, out var knownIp) && knownIp == ipAddress)
        {
            return;
        }

        // Try to add this MAC to the processing set. If checking fails (already there),
        // it means another thread is already processing this device. Drop the message.
        if (!_processingDevices.TryAdd(macAddress, 0))
        {
            return;
        }

        try
        {
            // Double-check: Check cache again after acquiring lock case another thread processed it
            if (_knownDevices.TryGetValue(macAddress, out knownIp) && knownIp == ipAddress)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BackupContext>();
            var settings = await context.Settings.FirstOrDefaultAsync();

            if (settings?.MqttAutoAdd == true)
            {
                // does a device with this mac address already exist?
                var foundDevice = await context.Devices
                    .Include(d => d.Interfaces)
                    .FirstOrDefaultAsync(d => d.Interfaces.Any(i => i.MacAddress == macAddress));

                if (foundDevice == null)
                {
                    var strategy = _strategies.FirstOrDefault(s => s.SupportedType == discovered.Type);
                    if (strategy == null)
                    {
                        _logger.LogWarning("No strategy found for device type {Type}", discovered.Type);
                        return;
                    }

                    // probe the device to get all information
                    var probedDevice = await strategy.ProbeAsync(ipAddress, _httpClientFactory.CreateClient());
                    if (probedDevice == null)
                    {
                        _logger.LogWarning("Failed to probe device {IpAddress}", ipAddress);
                        return;
                    }

                    // create new device or update existing if probed MAC matches an existing device
                    var probedPrimaryInterface = probedDevice.Interfaces.FirstOrDefault(i => !string.IsNullOrEmpty(i.MacAddress));
                    var probedMac = probedPrimaryInterface?.MacAddress;
                    var probedIp = probedPrimaryInterface?.IpAddress ?? ipAddress;

                    if (probedMac == null) return;

                    var existingDevice = await context.Devices
                        .Include(d => d.Interfaces)
                        .FirstOrDefaultAsync(d => d.Interfaces.Any(i => i.MacAddress == probedMac));

                    if (existingDevice != null)
                    {
                        // Device exists with the probed MAC, but was not found by discovered MAC
                        // This means discovered.MacAddress != probedMac
                        // We should update the existing device and ensure the cache prevents future lookups
                        existingDevice.Name = probedDevice.Name;
                        existingDevice.CurrentFirmwareVersion = probedDevice.FirmwareVersion;

                        var existingInterface = existingDevice.Interfaces.FirstOrDefault(i => i.MacAddress == probedMac);
                        if (existingInterface != null)
                        {
                            existingInterface.IpAddress = probedIp;
                        }
                        else
                        {
                            existingDevice.Interfaces.Add(new NetworkInterface { IpAddress = probedIp, MacAddress = probedMac });
                        }

                        await context.SaveChangesAsync();

                        // Update cache for the REAL mac
                        _knownDevices.AddOrUpdate(probedMac, probedIp, (k, v) => probedIp);
                        _logger.LogInformation("Updated existing device by probed MAC: {Name} {Mac} ({IpAddress})", existingDevice.Name, probedMac, probedIp);
                    }
                    else
                    {
                        var device = new Device
                        {
                            Name = probedDevice.Name,
                            Type = probedDevice.Type,
                            HardwareModel = probedDevice.HardwareModel,
                            CurrentFirmwareVersion = probedDevice.FirmwareVersion,
                            Interfaces = probedDevice.Interfaces
                        };

                        // add device to context
                        context.Devices.Add(device);
                        // save changes
                        await context.SaveChangesAsync();

                        foreach (var i in device.Interfaces)
                        {
                            if (!string.IsNullOrEmpty(i.MacAddress))
                                _knownDevices.AddOrUpdate(i.MacAddress, i.IpAddress, (k, v) => i.IpAddress);
                        }

                        _logger.LogInformation("Auto-added device via MQTT: {Name} {Type} ({IpAddress})", device.Name, device.Type, device.Interfaces.FirstOrDefault()?.IpAddress);
                    }

                    // Also update cache for the DISCOVERED mac address to avoid re-probing
                    if (macAddress != probedMac)
                    {
                        _knownDevices.AddOrUpdate(macAddress, probedIp, (k, v) => probedIp);
                    }
                }
                else
                {
                    var foundInterface = foundDevice.Interfaces.FirstOrDefault(i => i.MacAddress == macAddress);
                    if (foundInterface != null)
                    {
                        // Update cache for existing device even if no changes, so we skip next time
                        _knownDevices.AddOrUpdate(macAddress, foundInterface.IpAddress, (k, v) => foundInterface.IpAddress);
                    }

                    // same mac, same type, different ip address        
                    if (foundDevice.Type == discovered.Type && foundInterface?.IpAddress != ipAddress)
                    {
                        var strategy = _strategies.FirstOrDefault(s => s.SupportedType == discovered.Type);
                        if (strategy == null)
                        {
                            _logger.LogWarning("No strategy found for device type {Type}", discovered.Type);
                            return;
                        }

                        // probe the device to get all information
                        var probedDevice = await strategy.ProbeAsync(ipAddress, _httpClientFactory.CreateClient());
                        if (probedDevice == null)
                        {
                            _logger.LogWarning("Failed to probe device {IpAddress}", ipAddress);
                            return;
                        }

                        // update device ip address
                        if (probedDevice.Name == foundDevice.Name)
                        {
                            if (foundInterface != null)
                            {
                                foundInterface.IpAddress = ipAddress;
                            }

                            await context.SaveChangesAsync();

                            // Update cache
                            _knownDevices[macAddress] = ipAddress;

                            _logger.LogInformation("Updated device IP address via MQTT: {Name} ({IpAddress})", foundDevice.Name, ipAddress);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding discovered device");
        }
        finally
        {
            _processingDevices.TryRemove(macAddress, out _);
        }
    }

    private PeriodicTimer? _discoveryTimer;

    private async Task StartDiscoveryLoop()
    {
        _discoveryTimer?.Dispose();
        _discoveryTimer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await _discoveryTimer.WaitForNextTickAsync())
        {
            await PublishDiscoveryMessages();
        }
    }

    private async Task PublishDiscoveryMessages()
    {
        if (_mqttClient == null || !_mqttClient.IsConnected) return;

        try
        {
            foreach (var strategy in _mqttStrategies)
            {
                var msg = strategy.DiscoveryMessage;
                if (msg != null)
                {
                    await _mqttClient.InternalClient.PublishStringAsync(msg.Topic, msg.Payload);
                    _logger.LogInformation("Published discovery message for {Type}: {Topic} {Payload}", strategy.SupportedType, msg.Topic, msg.Payload);
                }
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing discovery messages");
        }
    }

    public void Dispose()
    {
        _discoveryTimer?.Dispose();
        _mqttClient?.Dispose();
    }
}
