using VRCDollyManager.Models;

namespace VRCDollyManager.Services.SteamVR;

public interface ISteamVrService 
{
    /// <summary>
    /// Raised whenever the SteamVR application manifest is added or removed.
    /// The <see cref="SteamVrManifestChangedEvent.IsManifestInstalled"/> flag indicates
    /// whether the manifest was registered (<c>true</c>) or unregistered (<c>false</c>).
    /// </summary>
    event EventHandler<SteamVrManifestChangedEvent>? ManifestChanged;

    /// <summary>
    ///  Raised whenever the SteamVR application state changes.
    /// The <see cref="SteamVrMonitorChangeEvent.Connected"/> flag indicates
    /// whether the openvr is connected (<c>true</c>) or disconnected (<c>false</c>).
    /// </summary>
    public event EventHandler<SteamVrMonitorChangeEvent>? StateChanged;
    
    /// <summary>
    /// Checks whether the VR application identified by <see cref="AppConstants.VrApplicationName"/>
    /// is currently registered with SteamVR.
    /// </summary>
    /// <param name="isInstalled">
    /// When this method returns, set to <c>true</c> if the manifest is installed; otherwise <c>false</c>.
    /// </param>
    /// <returns><c>true</c> if the check could be performed; <c>false</c> on error.</returns>
    bool TryIsInstalled(out bool isInstalled);

    /// <summary>
    /// Attempts to register this application's manifest with SteamVR so it will auto-launch.
    /// Also enables auto-launch of the application in SteamVR.
    /// </summary>
    /// <returns><c>true</c> if registration succeeded; <c>false</c> otherwise.</returns>
    bool TryRegister();

    /// <summary>
    /// Attempts to unregister this application's manifest from SteamVR and disable auto-launch.
    /// </summary>
    /// <returns><c>true</c> if unregistration succeeded; <c>false</c> otherwise.</returns>
    bool TryUnregister();

    /// <summary>
    /// Defines if OpenVr is connected (<c>true</c>) or disconnected (<c> false </c>)
    /// </summary>
    /// <returns><c>true</c> if connected; <c>false</c> if disconnected.</returns>
    public bool IsInitialized { get; }

}