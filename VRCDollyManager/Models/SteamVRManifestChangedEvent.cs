namespace VRCDollyManager.Models;

public class SteamVrManifestChangedEvent(bool isManifestInstalled) : EventArgs
{
    public bool IsManifestInstalled { get; init; } = isManifestInstalled;
}