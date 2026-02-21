using System.Collections.Concurrent;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services.Strategies;
using Makaretu.Dns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HomeRecall.Services;

public class MdnsScanner : BackgroundService
{
    private readonly ILogger<MdnsScanner> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IMdnsDeviceStrategy> _strategies;
    private readonly IMemoryCache _cache;
    private MulticastService? _mdns;
    private bool _mdnsEnabled = true;

    public MdnsScanner(
        ILogger<MdnsScanner> logger,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IDeviceStrategy> strategies,
        IMemoryCache cache)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _strategies = strategies.OfType<IMdnsDeviceStrategy>();
        _cache = cache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("mDNS Scanner service starting up.");

        _mdns = new MulticastService();
        _mdns.AnswerReceived += OnMdnsMessageReceived;

        try
        {
            _mdns.Start();
            _logger.LogInformation("mDNS MulticastService started successfully.");

            DateTime lastActiveSweep = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                // Verify if mDNS is currently enabled in settings (check every 15s to react to UI changes)
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<BackupContext>();
                    var settings = await dbContext.Settings.FirstOrDefaultAsync(cancellationToken: stoppingToken);
                    if (settings != null && settings.MdnsEnabled != _mdnsEnabled)
                    {
                        var oldState = _mdnsEnabled ? "enabled" : "disabled";
                        var newState = settings.MdnsEnabled ? "enabled" : "disabled";
                        _logger.LogInformation($"mDNS Discovery toggled from {oldState} to {newState}.");
                        _mdnsEnabled = settings.MdnsEnabled;
                    }
                }

                // If enabled, send an active query sweep every 5 minutes to proactively find devices
                if (_mdnsEnabled && (DateTime.UtcNow - lastActiveSweep).TotalMinutes >= 5)
                {
                    lastActiveSweep = DateTime.UtcNow;
                    _logger.LogTrace("Sending active mDNS query sweep...");
                    foreach (var strategy in _strategies)
                    {
                        foreach (var serviceType in strategy.MdnsServiceTypes)
                        {
                            try
                            {
                                _mdns?.SendQuery(serviceType);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogTrace(ex, $"Failed to send mDNS query for type {serviceType}");
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start mDNS MulticastService. Ensure UDP port 5353 is available.");
        }
        finally
        {
            _mdns?.Stop();
        }
    }

    private async void OnMdnsMessageReceived(object? sender, MessageEventArgs e)
    {
        if (!_mdnsEnabled) return;

        try
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var ptrs = e.Message.Answers.OfType<PTRRecord>()
                    .Concat(e.Message.AdditionalRecords.OfType<PTRRecord>())
                    .Select(p => p.DomainName.ToString());

                _logger.LogTrace($"mDNS Received from {e.RemoteEndPoint}. Answers: {e.Message.Answers.Count}, Additional: {e.Message.AdditionalRecords.Count}, PTRs: {string.Join(", ", ptrs)}");
            }

            DiscoveredDevice? discoveredDevice = null;
            IMdnsDeviceStrategy? matchedStrategy = null;

            foreach (var strategy in _strategies)
            {
                discoveredDevice = strategy.DiscoverFromMdns(e);
                if (discoveredDevice != null)
                {
                    _logger.LogDebug($"mDNS matched strategy '{strategy.GetType().Name}' for device '{discoveredDevice.Name}'");
                    matchedStrategy = strategy;
                    break;
                }
            }

            if (discoveredDevice == null || discoveredDevice.Interfaces.Count == 0 || matchedStrategy == null)
            {
                if (_logger.IsEnabled(LogLevel.Trace) && e.Message.Answers.Count > 0)
                {
                    _logger.LogTrace("mDNS message ignored: No matching strategy or incomplete device info.");
                }
                return;
            }

            var netInterface = discoveredDevice.Interfaces.First();
            if (string.IsNullOrEmpty(netInterface.IpAddress)) return;

            string idKey = string.IsNullOrEmpty(netInterface.MacAddress) ? netInterface.Hostname! : netInterface.MacAddress;
            string cacheKey = $"mdns_seen_{matchedStrategy.SupportedType}_{idKey}_{netInterface.IpAddress}";

            if (_cache.TryGetValue(cacheKey, out _))
            {
                // We have recently processed this exact device with this exact IP
                return;
            }

            // Cache for 10 minutes to prevent DB hammering
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));

            _logger.LogTrace($"mDNS discovered: {discoveredDevice.Type} - {discoveredDevice.Name} at {netInterface.IpAddress} (MAC: {netInterface.MacAddress})");

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BackupContext>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            // Attempt to fetch full details actively via ProbeAsync
            try
            {
                var httpClient = httpClientFactory.CreateClient("DeviceScanner");
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var probedDevice = await matchedStrategy.ProbeAsync(netInterface.IpAddress, httpClient);

                if (probedDevice != null && probedDevice.Interfaces.Count > 0)
                {
                    _logger.LogTrace($"mDNS active probe succeeded for {netInterface.IpAddress}. Overwriting mDNS partial info.");
                    discoveredDevice = probedDevice;
                    var probedInterface = discoveredDevice.Interfaces.First();
                    if (string.IsNullOrEmpty(probedInterface.IpAddress))
                    {
                        probedInterface.IpAddress = netInterface.IpAddress;
                    }
                    netInterface = probedInterface;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to actively probe mDNS device at {IpAddress}, falling back to mDNS info.", netInterface.IpAddress);
            }

            // Search for existing device
            Device? existingDevice = null;

            if (!string.IsNullOrEmpty(netInterface.MacAddress))
            {
                var normMac = netInterface.MacAddress.Replace(":", "").ToUpperInvariant();
                var macMatch = await dbContext.NetworkInterfaces
                    .Include(i => i.Device)
                        .ThenInclude(d => d.Interfaces)
                    .Where(i => i.MacAddress != null && i.MacAddress.ToUpper().Replace(":", "") == normMac)
                    .FirstOrDefaultAsync();

                if (macMatch?.Device != null)
                {
                    existingDevice = macMatch.Device;
                }
            }

            if (existingDevice == null)
            {
                // Fallback to hostname matching if no MAC was available or matched
                if (string.IsNullOrEmpty(netInterface.MacAddress) && !string.IsNullOrEmpty(netInterface.Hostname))
                {
                    // WLED/Shelly hostnames might match the device name
                    existingDevice = await dbContext.Devices
                        .Include(d => d.Interfaces)
                        .FirstOrDefaultAsync(d => d.Name == discoveredDevice.Name || d.Interfaces.Any(i => i.Hostname == netInterface.Hostname));
                }
            }

            if (existingDevice != null)
            {
                // Update IP if it has changed
                var existingInterface = existingDevice.Interfaces.FirstOrDefault(i =>
                    (i.MacAddress != null && i.MacAddress.Replace(":", "").ToUpperInvariant() == netInterface.MacAddress?.Replace(":", "").ToUpperInvariant()) ||
                    (i.Type == NetworkInterfaceType.Wifi)); // Default fallback

                // If it's the exact matching interface by MAC or just picking the first Wifi interface
                if (existingInterface == null && existingDevice.Interfaces.Count > 0)
                {
                    existingInterface = existingDevice.Interfaces.FirstOrDefault();
                }

                if (existingInterface != null && existingInterface.IpAddress != netInterface.IpAddress)
                {
                    _logger.LogInformation($"mDNS: Updating IP for {existingDevice.Name} from {existingInterface.IpAddress} to {netInterface.IpAddress}");
                    existingInterface.IpAddress = netInterface.IpAddress;
                    if (!string.IsNullOrEmpty(netInterface.Hostname) && string.IsNullOrEmpty(existingInterface.Hostname))
                    {
                        existingInterface.Hostname = netInterface.Hostname;
                    }
                    if (string.IsNullOrEmpty(existingDevice.CurrentFirmwareVersion) && !string.IsNullOrEmpty(discoveredDevice.FirmwareVersion))
                    {
                        existingDevice.CurrentFirmwareVersion = discoveredDevice.FirmwareVersion;
                    }
                    if (string.IsNullOrEmpty(existingDevice.HardwareModel) && !string.IsNullOrEmpty(discoveredDevice.HardwareModel))
                    {
                        existingDevice.HardwareModel = discoveredDevice.HardwareModel;
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
            else
            {
                // It's a completely new device! Auto-add it.
                _logger.LogInformation($"mDNS: Auto-adding new device {discoveredDevice.Name} ({netInterface.IpAddress})");

                var newEntity = new Device
                {
                    Name = discoveredDevice.Name,
                    Type = discoveredDevice.Type,
                    CurrentFirmwareVersion = discoveredDevice.FirmwareVersion,
                    HardwareModel = discoveredDevice.HardwareModel
                };

                newEntity.Interfaces.Add(new NetworkInterface
                {
                    IpAddress = netInterface.IpAddress,
                    MacAddress = netInterface.MacAddress,
                    Hostname = netInterface.Hostname,
                    Type = netInterface.Type
                });

                dbContext.Devices.Add(newEntity);
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error processing mDNS message.");
        }
    }
}
