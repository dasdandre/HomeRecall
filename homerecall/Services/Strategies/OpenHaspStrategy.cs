namespace HomeRecall.Services;

public class OpenHaspStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenHasp;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();

        try 
        {
            var config = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/config.json");
            files.Add(new("config.json", config));
        }
        catch { /* Log if necessary via ILogger injection, or just ignore optional files */ }

        try
        {
            var pages = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/pages.jsonl");
            files.Add(new("pages.jsonl", pages));
        }
        catch { }

        return files;
    }
}