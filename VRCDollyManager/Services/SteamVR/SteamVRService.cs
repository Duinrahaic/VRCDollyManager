using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Valve.VR;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services.SteamVR
{
    /// <summary>
    /// A hosted background service that manages SteamVR integration via OpenVR.
    /// It can check installation status, register/unregister your app's VR manifest,
    /// and polls for quit events to trigger a shutdown callback.
    /// </summary>
    public class SteamVrService : ISteamVrService, IDisposable
    {
        /// <summary>
        /// Raised whenever the SteamVR application manifest is added or removed.
        /// The <see cref="SteamVrManifestChangedEvent.IsManifestInstalled"/> flag indicates
        /// whether the manifest was registered (<c>true</c>) or unregistered (<c>false</c>).
        /// </summary>
        public event EventHandler<SteamVrManifestChangedEvent>? ManifestChanged;
        /// <summary>
        ///  Raised whenever the SteamVR application state changes.
        /// The <see cref="SteamVrMonitorChangeEvent.Connected"/> flag indicates
        /// whether the openvr is connected (<c>true</c>) or disconnected (<c>false</c>).
        /// </summary>
        public event EventHandler<SteamVrMonitorChangeEvent>? StateChanged;

        private CVRSystem? _cVr;
        private readonly ILogger<SteamVrService> _logger;
        private Task? _pollingTask;
        private CancellationTokenSource? _cts;
        
        /// <summary>
        /// Defines if OpenVr is connected (<c>true</c>) or disconnected (<c> false </c>)
        /// </summary>
        /// <returns><c>true</c> if connected; <c>false</c> if disconnected.</returns>
        public bool IsInitialized { get; private set; } = false;
        

        /// <summary>
        /// Creates a new instance of <see cref="SteamVrService"/>.
        /// </summary>
        /// <param name="logger">The logger instance to record informational and error messages.</param>
        public SteamVrService(ILogger<SteamVrService> logger)
        {
            _logger = logger;
            _logger.LogInformation("SteamVrService Started.");
            StartPolling();
        }

        /// <summary>
        /// Checks whether the VR application identified by <see cref="AppConstants.VrApplicationName"/>
        /// is currently registered with SteamVR.
        /// </summary>
        /// <param name="isInstalled">
        /// When this method returns, set to <c>true</c> if the manifest is installed; otherwise <c>false</c>.
        /// </param>
        /// <returns><c>true</c> if the check could be performed; <c>false</c> on error.</returns>
        public bool TryIsInstalled(out bool isInstalled)
        {
            isInstalled = false;
            try
            {
                if (OpenVR.System == null)
                {
                    return false;
                }

                isInstalled = OpenVR.Applications.IsApplicationInstalled(AppConstants.VrApplicationName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to verify registration state with SteamVR");
                return false;
            }
        }

        /// <summary>
        /// Attempts to register this application's manifest with SteamVR so it will auto-launch.
        /// Also enables auto-launch of the application in SteamVR.
        /// </summary>
        /// <returns><c>true</c> if registration succeeded; <c>false</c> otherwise.</returns>
        public bool TryRegister()
        {
            try
            {
                if (TryIsInstalled(out var isInstalled))
                {
                    if (isInstalled)
                    {
                        _logger.LogError("Unable to register with SteamVR: Manifest is already installed.");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Unable to register manifest. Unable to communicate with SteamVR.");
                    return false;
                }

                string executablePath = Application.ExecutablePath;
                string executableDir = Path.GetDirectoryName(executablePath)!;

                EVRApplicationError error = OpenVR.Applications.AddApplicationManifest(
                    Path.Join(executableDir, "manifest.vrmanifest"),
                    false);

                if (error != EVRApplicationError.None)
                {
                    _logger.LogError($"Unable to register with SteamVR: {error}");
                    return false;
                }

                // Enable SteamVR auto-launch for this app
                OpenVR.Applications.SetApplicationAutoLaunch(AppConstants.VrApplicationName, true);

                _logger.LogInformation($"{AppConstants.VrApplicationName} is registered successfully.");
                ManifestChanged?.Invoke(this, new SteamVrManifestChangedEvent(true));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while trying to register with SteamVR");
                return false;
            }
        }

        /// <summary>
        /// Attempts to unregister this application's manifest from SteamVR and disable auto-launch.
        /// </summary>
        /// <returns><c>true</c> if unregistration succeeded; <c>false</c> otherwise.</returns>
        public bool TryUnregister()
        {
            try
            {
                if (TryIsInstalled(out var isInstalled))
                {
                    if (!isInstalled)
                    {
                        _logger.LogError("Unable to unregister with SteamVR: Manifest is not installed.");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Unable to unregister manifest. Unable to communicate with SteamVR.");
                    return false;
                }

                var sb = new StringBuilder(512);
                EVRApplicationError error = EVRApplicationError.None;

                OpenVR.Applications.GetApplicationPropertyString(
                    AppConstants.VrApplicationName,
                    EVRApplicationProperty.WorkingDirectory_String,
                    sb, (uint)sb.Capacity, ref error);

                string manifestPath = Path.Join(sb.ToString(), "manifest.vrmanifest");
                error = OpenVR.Applications.RemoveApplicationManifest(manifestPath);

                if (error != EVRApplicationError.None)
                {
                    _logger.LogError($"Unable to unregister with SteamVR: {error}");
                    return false;
                }

                _logger.LogInformation($"{AppConstants.VrApplicationName} is unregistered successfully.");
                ManifestChanged?.Invoke(this, new SteamVrManifestChangedEvent(false));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while trying to unregister with SteamVR");
                return false;
            }
        }

        /// <summary>
        /// Handles a SteamVR quit event by performing an application shutdown.
        /// </summary>
        private void Shutdown()
        {
            _logger.LogInformation("Shutdown requested by SteamVR. VR mode: " + (AppFlags.IsVRMode ?  "Enabled" : "Disabled"));
            StateChanged?.Invoke(this, new SteamVrMonitorChangeEvent(false));
            if (!AppFlags.IsVRMode) return;
            StopPolling();
            _logger.LogError("Shutdown requested by SteamVR.");
            Environment.Exit(0);
        }

        /// <summary>
        /// Initializes the OpenVR system for background polling of VR events.
        /// </summary>
        private void Initialize()
        {
            EVRInitError error = EVRInitError.None;
            _cVr = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
            if (error == EVRInitError.None)
            {
                _logger.LogInformation("OpenVR initialized successfully.");
                IsInitialized = true;
                StateChanged?.Invoke(this, new SteamVrMonitorChangeEvent(true));
            }
        }

        /// <summary>
        /// Continuously polls SteamVR events and triggers <see cref="Shutdown"/> on quit.
        /// </summary>
        /// <param name="stoppingToken">
        /// A cancellation token that signals when the host is stopping.
        /// </param>
        /// <returns>A task that completes when the service is stopped.</returns>
        private void PollSteamVrEvents(CancellationToken token)
        {
            VREvent_t evt = new VREvent_t();
            uint eventSize = (uint)Marshal.SizeOf(evt);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!IsInitialized)
                    {
                        Initialize();
                    }
                    else if (IsInitialized 
                             && OpenVR.System != null 
                             && OpenVR.System.PollNextEvent(ref evt, eventSize))
                    {
                        if (evt.eventType == (uint)EVREventType.VREvent_Quit)
                        {
                            Shutdown();
                        }
                    }
                }
                catch
                {
                    // Ignore polling errors
                }

                Thread.Sleep(100);
            }
        }
        
        private void StartPolling()
        {
            if (_pollingTask is { IsCompleted: false })
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _pollingTask = Task.Run(() => PollSteamVrEvents(token), token);
        }

        private void StopPolling()
        {
            if (_cts is { IsCancellationRequested: false })
            {
                _cts.Cancel();
            }
        }

        public void Dispose()
        {
            StopPolling();
        }
    }
}
