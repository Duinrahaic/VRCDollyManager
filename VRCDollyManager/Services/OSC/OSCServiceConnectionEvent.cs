namespace VRCDollyManager.Services.OSC;

public class OSCServiceConnectionEvent(bool connected, int? listeningPort = null ) : EventArgs
{
    public bool Connected { get; init; } = connected;
    public int? ListeningPort { get; init; } = listeningPort;

}