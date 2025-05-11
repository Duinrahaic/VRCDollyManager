using System.Net;
using LucHeart.CoreOSC;
using OscQueryLibrary;
using OscQueryLibrary.Utils;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services.OSC;

public class OscService : IDisposable, IOscService
{
    public delegate void OscSubscriptionEventHandler(OscSubscriptionEvent e);

    public event OscSubscriptionEventHandler? OnOscMessageReceived;

    public delegate void ConnectionStateChangedHandler(bool isConnected);

    public event ConnectionStateChangedHandler? OnConnectionStateChanged;

    private readonly ILogger<OscService> _logger;
    private readonly CancellationTokenSource _cts;
    private OscQueryServer? _server;
    private OscDuplex? _connection;
    private CancellationTokenSource _loopCancellationToken = new();
    private OscQueryServer? _currentOscQueryServer = null;
    private bool _isConnected;
    private bool _isReconnecting = false;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                OnConnectionStateChanged?.Invoke(_isConnected);
                _logger.LogInformation(
                    $"OSC Connection state changed: {(_isConnected ? "Connected" : "Disconnected")}");

                if (!_isConnected) StartReconnectLoop();
            }
        }
    }

    public OscService(ILogger<OscService> logger)
    {
        _logger = logger;
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Initialized OSCService");

        if (_server != null) Stop();

        Start();
    }

    public void RestartService()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        try
        {
            _server?.Dispose();
            _server = null;
            _connection?.Dispose();
            _connection = null;
            IsConnected = false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error stopping OSC: {ex.Message}");
        }
    }

    public void Start()
    {
        if (_connection != null)
        {
            _logger.LogWarning("OSC Service already started");
            return;
        }

        try
        {
            if (OperatingSystem.IsBrowser()) return;
            _server = new OscQueryServer("VDM", IPAddress.Loopback);
            _server.FoundVrcClient += FoundVrcClient;
            _server.Start();
            _logger.LogInformation($"OSC Query Server Started");
        }
        catch (Exception ex)
        {
            _logger.LogError($"OSC Failed: {ex.Message}");
        }
    }



    private async Task FoundVrcClient(OscQueryServer oscQueryServer, IPEndPoint ipEndPoint)
    {
        _loopCancellationToken.Cancel();
        _loopCancellationToken = new CancellationTokenSource();
        _connection?.Dispose();
        _connection = null;

        _logger.LogInformation("Found VRC client at {EndPoint}", ipEndPoint);
        _logger.LogInformation("Starting listening for VRC client at {Port}", oscQueryServer.OscReceivePort);
        _connection = new OscDuplex(new IPEndPoint(ipEndPoint.Address, oscQueryServer.OscReceivePort), ipEndPoint);

        _currentOscQueryServer = oscQueryServer;
        IsConnected = true; // Set connected state
        _isReconnecting = false; // Stop reconnect attempts

        AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();
        ErrorHandledTask.Run(ReceiverLoopAsync);
    }
    
    private void Cleanup()
    {
        _currentOscQueryServer?.Dispose();
        _currentOscQueryServer = null;
        _connection?.Dispose();
        _connection = null;
        IsConnected = false;
    }

    private async Task ReceiverLoopAsync()
    {
        var currentCancellationToken = _loopCancellationToken.Token;
        while (!currentCancellationToken.IsCancellationRequested)
            try
            {
                if (_connection != null)
                {
                    var received = await _connection.ReceiveMessageAsync();
                    if (received.Address.Contains("VDM"))
                    {
                        var message = received;
                        _ = Task.Run(() => OnOscMessageReceived?.Invoke(new OscSubscriptionEvent(message)),
                            currentCancellationToken);
                    }
                    else if (received.Address.Contains("dolly"))
                    {
                        
                    }
                }
                else
                {
                    IsConnected = false;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error in receiver loop", e);
                IsConnected = false;
            }

        IsConnected = false;
    }

    public void SendMessage(string address, params object?[]? args)
    {
        if (OperatingSystem.IsBrowser()) return;
        if (string.IsNullOrEmpty(address)) throw new NullReferenceException("address cannot be null or empty");
        if (args == null) return;
        if (_connection == null)
        {
            IsConnected = false;
            return;
        }

        var message = new OscMessage(address, args);
        try
        {
            _connection?.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error occurred while sending OSC message: {ex}");
            IsConnected = false;
        }
    }

    private async void StartReconnectLoop()
    {
        if (_isReconnecting) return;
        _isReconnecting = true;

        while (!IsConnected && !_cts.IsCancellationRequested)
        {
            _logger.LogInformation("Reconnecting in 15 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(15), _cts.Token);

            if (!_cts.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting to reconnect...");
                Start(); // Restart the service
            }
        }

        _isReconnecting = false;
    }

    private void ReleaseUnmanagedResources()
    {
        if (_server != null) _server.FoundVrcClient -= FoundVrcClient;
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
            _server?.Dispose();
            _loopCancellationToken.Dispose();
            _currentOscQueryServer?.Dispose();
            _connection?.Dispose();
        }

        IsConnected = false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~OscService()
    {
        Dispose(false);
    }
}