namespace VRCDollyManager.Services.SteamVR;

public class SteamVrMonitorChangeEvent(bool connected) : EventArgs
{
    public bool Connected { get; init; } = connected;
}