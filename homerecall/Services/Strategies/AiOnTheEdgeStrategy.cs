namespace HomeRecall.Services;

public class AiOnTheEdgeStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.AiOnTheEdge;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();

        var configIni = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/fileserver/config/config.ini");
        files.Add(new("config/config.ini", configIni));

        string[] potentialImages = { "config/ref0.jpg", "config/ref1.jpg", "config/reference.jpg" };
        foreach(var imgPath in potentialImages)
        {
            try 
            {
                var imgData = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/fileserver/{imgPath}");
                files.Add(new(imgPath, imgData));
            }
            catch 
            {
                // Ignore missing images
            }
        }
        return files;
    }
}