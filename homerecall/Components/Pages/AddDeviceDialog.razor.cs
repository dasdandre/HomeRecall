using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class AddDeviceDialog : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDeviceScanner Scanner { get; set; } = null!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private Device model = new Device();
    private bool success;
    private bool _isScanning;

    private async Task<string?> ValidateIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return L["AddDevice_IpRequired"];
        if (!System.Net.IPAddress.TryParse(ip, out _)) return L["AddDevice_InvalidIp"];
        
        if (await Context.Devices.AnyAsync(d => d.IpAddress == ip))
        {
            return L["AddDevice_AlreadyExists"];
        }
        
        return null;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(model.IpAddress)) return;

        // Check if already exists
        if (await Context.Devices.AnyAsync(d => d.IpAddress == model.IpAddress))
        {
            Snackbar.Add(L["AddDevice_AlreadyExists"], Severity.Warning);
            return;
        }

        _isScanning = true;
        try
        {
            var results = await Scanner.ScanNetworkAsync(model.IpAddress, model.IpAddress, new[] { model.Type }.ToList());
            var discovered = results.FirstOrDefault();

            if (discovered != null)
            {
                // Verify type
                if (discovered.Type == model.Type)
                {
                    model.Name = discovered.Name;
                    model.Hostname = discovered.Hostname;
                    model.MacAddress = discovered.MacAddress;
                    model.HardwareModel = discovered.HardwareModel;
                    model.CurrentFirmwareVersion = discovered.FirmwareVersion;

                    Context.Devices.Add(model);
                    await Context.SaveChangesAsync();
                    Snackbar.Add(L["AddDevice_Success"], Severity.Success);
                    MudDialog.Close(DialogResult.Ok(true));
                }
                else
                {
                    Snackbar.Add(L["AddDevice_TypeMismatch", discovered.Type], Severity.Error);
                }
            }
            else
            {
                Snackbar.Add(L["AddDevice_NotFound", model.IpAddress], Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Discovery failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isScanning = false;
        }
    }
}
