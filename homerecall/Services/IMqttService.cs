namespace HomeRecall.Services;

public enum MqttConnectionStatus
{
    Disabled,
    Disconnected,
    Connecting,
    Connected,
    Error
}

public interface IMqttService
{
    MqttConnectionStatus Status { get; }
    string? LastErrorMessage { get; }
    event Action StatusChanged;
    
    Task ReconnectAsync();
}
