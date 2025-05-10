namespace VRCDollyManager.Models;

public class SteamVrMonitorChangeEvent(bool connected) : EventArgs
{
    public bool Connected { get; init; } = connected;
}