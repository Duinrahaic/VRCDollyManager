namespace VRCDollyManager.Services.OSC;

public interface IOscService
{
    event OscService.OscSubscriptionEventHandler? OnOscMessageReceived;
    event OscService.ConnectionStateChangedHandler<OSCServiceConnectionEvent>? OnConnectionStateChanged;
    bool IsConnected { get; }
    void RestartService();
    void Stop();
    void Start();
    void SendMessage(string address, params object?[]? args);
    void Dispose();
}