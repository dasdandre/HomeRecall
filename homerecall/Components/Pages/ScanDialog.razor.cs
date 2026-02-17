using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;
using HomeRecall.Services.Strategies;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class ScanDialog : ComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private IDeviceScanner Scanner { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private string _startIp = "192.168.1.1";
    private int _endSuffix = 254;
    private bool _isValid;
    private bool _scanRunning = false;
    private int _progressPercent = 0;
    private int _foundCount = 0;
    private int _plannedScanCount = 0;
    private int _skippedCount = 0;

    private List<DeviceType> _allTypes = Enum.GetValues(typeof(DeviceType)).Cast<DeviceType>().ToList();
    private List<DeviceType> _selectedTypes = Enum.GetValues(typeof(DeviceType)).Cast<DeviceType>().ToList();

    private List<DiscoveredDevice>? _foundDevices;
    private HashSet<DiscoveredDevice> _selectedItems = new();
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        var settings = await Context.Settings.FindAsync(1);
        if (settings != null)
        {
            if (!string.IsNullOrEmpty(settings.LastScanIpStart))
            {
                _startIp = settings.LastScanIpStart;
                _endSuffix = settings.LastScanIpEndSuffix;
            }

            if (!string.IsNullOrEmpty(settings.LastScanDeviceTypes))
            {
                try
                {
                    var types = settings.LastScanDeviceTypes.Split(',')
                        .Select(t => Enum.Parse<DeviceType>(t))
                        .ToList();
                    if (types.Any()) _selectedTypes = types;
                }
                catch { /* Ignore parsing errors */ }
            }
        }
    }

    private string? ValidateStartIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "Required";
        if (!System.Net.IPAddress.TryParse(ip, out _)) return "Invalid IP";

        var parts = ip.Split('.');
        if (parts.Length == 4 && int.TryParse(parts[3], out int startSuffix))
        {
            if (startSuffix > _endSuffix) return "Start > End";
        }

        return null;
    }

    private string GetEndIp()
    {
        if (string.IsNullOrWhiteSpace(_startIp)) return "";
        var parts = _startIp.Split('.');
        if (parts.Length != 4) return "";
        return $"{parts[0]}.{parts[1]}.{parts[2]}.{_endSuffix}";
    }

    private void Reset()
    {
        _foundDevices = null;
        _scanRunning = false;
        _cts?.Cancel();
    }

    private async Task StartScan()
    {
        if (!_selectedTypes.Any())
        {
            Snackbar.Add("Select at least one device type", Severity.Warning);
            return;
        }

        _scanRunning = true;
        _foundDevices = new List<DiscoveredDevice>();
        _cts = new CancellationTokenSource();

        // Save settings for next time
        var settings = await Context.Settings.FindAsync(1);
        if (settings != null)
        {
            settings.LastScanIpStart = _startIp;
            settings.LastScanIpEndSuffix = _endSuffix;
            settings.LastScanDeviceTypes = string.Join(",", _selectedTypes);
            await Context.SaveChangesAsync();
        }

        _progressPercent = 0;
        _foundCount = 0;
        StateHasChanged();

        var progressHandler = new Progress<ScanProgressReport>(report =>
        {
            _progressPercent = report.Percent;
            _foundCount = report.FoundCount;
            if (report.LatestDevice != null)
            {
                if (_foundDevices != null && !_foundDevices.Any(d => d.IpAddress == report.LatestDevice.IpAddress))
                {
                    _foundDevices.Add(report.LatestDevice);
                    _selectedItems.Add(report.LatestDevice); // Auto-select new items
                }
            }
            StateHasChanged();
        });

        try
        {
            string endIp = GetEndIp();

            var knownIps = await Context.Devices.Select(d => d.IpAddress).ToListAsync();

            var (planned, skipped) = CalculatePlannedAndSkippedCount(_startIp, endIp, knownIps);
            _plannedScanCount = planned;
            _skippedCount = skipped;

            // Pass Cancellation Token
            var results = await Scanner.ScanNetworkAsync(_startIp, endIp, _selectedTypes.ToList(), progressHandler, knownIps, _cts.Token);

            // Final consistency check (optional, but good practice)
            if (results != null)
            {
                // Merge just in case
                foreach (var r in results)
                {
                    if (!_foundDevices.Any(d => d.IpAddress == r.IpAddress))
                        _foundDevices.Add(r);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancel
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Scan failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _scanRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void StopScan()
    {
        _cts?.Cancel();
    }

    private Task OnSelectedValuesChanged(IEnumerable<DeviceType> values)
    {
        _selectedTypes = values?.ToList() ?? new List<DeviceType>();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private (int planned, int skipped) CalculatePlannedAndSkippedCount(string startIp, string endIp, IEnumerable<string>? knownIps)
    {
        if (!System.Net.IPAddress.TryParse(startIp, out var ipStart) || !System.Net.IPAddress.TryParse(endIp, out var ipEnd))
            return (0, 0);

        byte[] startBytes = ipStart.GetAddressBytes();
        byte[] endBytes = ipEnd.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(startBytes);
            Array.Reverse(endBytes);
        }

        uint startVal = BitConverter.ToUInt32(startBytes, 0);
        uint endVal = BitConverter.ToUInt32(endBytes, 0);
        if (endVal < startVal) return (0, 0);

        // Safety cap used in scanner: limit range to 513 addresses
        if (endVal - startVal > 512) endVal = startVal + 512;

        int total = (int)(endVal - startVal + 1);
        int skipped = 0;
        if (knownIps != null)
        {
            var knownSet = new HashSet<string>(knownIps);
            for (uint i = startVal; i <= endVal; i++)
            {
                byte[] bytes = BitConverter.GetBytes(i);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                var ip = new System.Net.IPAddress(bytes).ToString();
                if (knownSet.Contains(ip)) skipped++;
            }
        }

        return (total - skipped, skipped);
    }

    private async Task AddSelected()
    {
        // Stop the scan first if running
        StopScan();

        int addedCount = 0;
        foreach (var d in _selectedItems)
        {
            if (!Context.Devices.Any(x => x.IpAddress == d.IpAddress))
            {
                Context.Devices.Add(new Device
                {
                    Name = d.Name,
                    IpAddress = d.IpAddress,
                    Type = d.Type,
                    MacAddress = d.MacAddress,
                    Hostname = d.Hostname,
                    CurrentFirmwareVersion = d.FirmwareVersion,
                    HardwareModel = d.HardwareModel
                });
                addedCount++;
            }
        }

        await Context.SaveChangesAsync();
        Snackbar.Add($"{addedCount} devices added", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        StopScan();
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
