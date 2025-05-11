namespace VRCDollyManager.Services.SteamVR;

public class SteamVrManifestChangedEvent(bool isManifestInstalled) : EventArgs
{
    public bool IsManifestInstalled { get; init; } = isManifestInstalled;
}