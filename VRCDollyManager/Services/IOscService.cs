namespace VRCDollyManager.Services;

public interface IOscService
{
    event OscService.OscSubscriptionEventHandler? OnOscMessageReceived;
    event OscService.ConnectionStateChangedHandler? OnConnectionStateChanged;
    bool IsConnected { get; }
    void RestartService();
    void Stop();
    void Start();
    void SendMessage(string address, params object?[]? args);
    void Dispose();
}