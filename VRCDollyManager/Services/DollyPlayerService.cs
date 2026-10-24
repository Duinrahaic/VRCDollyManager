using VRCDollyManager.Extensions;
using VRCDollyManager.Models;
using VRCDollyManager.Services.OSC;

namespace VRCDollyManager.Services;

/// <summary>
/// Drives VRChat's camera dolly over OSC: importing paths, starting and stopping playback,
/// and tracking the playback and Look-At-Me state VRChat reports back.
/// </summary>
public class DollyPlayerService : IDisposable
{
    public const int MaxDelay = 5;

    private readonly IOscService _osc;
    private bool _relative;
    private int _delay;

    /// <summary>Raised when <see cref="IsPlaying"/>, <see cref="Relative"/> or <see cref="Delay"/> changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised with VRChat's reported playback state every time it sends <c>/dolly/Play</c>.</summary>
    public event EventHandler<bool>? PlaybackChanged;

    public DollyPlayerService(IOscService osc)
    {
        _osc = osc;
        _osc.OnOscMessageReceived += OnOscMessageReceived;
    }

    public bool IsPlaying { get; private set; }

    /// <summary>The camera's Look-At-Me state as last reported by VRChat, or null if it hasn't reported one yet.</summary>
    public bool? LookAtMe { get; private set; }

    /// <summary>Load dollies relative to the player instead of at their recorded world position.</summary>
    public bool Relative
    {
        get => _relative;
        set
        {
            _relative = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Seconds VRChat waits before starting playback.</summary>
    public int Delay
    {
        get => _delay;
        set
        {
            _delay = Math.Clamp(value, 0, MaxDelay);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ImportAsync(Dolly dolly)
    {
        if (!Relative)
        {
            _osc.SendMessage("/dolly/Import", dolly.GetDollyFilePath());
            return;
        }

        var path = dolly.GetRelativeDollyKeyframes();
        if (path == null) return;
        _osc.SendMessage("/dolly/Import", path);
        await Task.Delay(100);
        File.Delete(path);
    }

    /// <summary>Starts playback after <see cref="Delay"/>.</summary>
    public void Play() => _osc.SendMessage("/dolly/PlayDelayed", (float)Delay);

    /// <summary>Starts playback immediately, skipping <see cref="Delay"/>.</summary>
    public void PlayNow() => _osc.SendMessage("/dolly/Play", true);

    public void Stop() => _osc.SendMessage("/dolly/Play", false);

    public void SetLookAtMe(bool enabled) => _osc.SendMessage("/usercamera/LookAtMe", enabled);

    /// <summary>
    /// Waits until VRChat reports the given playback state. Returns false if it doesn't within <paramref name="timeout"/>.
    /// Call before sending the command that should cause the change so the report can't be missed.
    /// </summary>
    public async Task<bool> WaitForPlaybackAsync(bool playing, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, bool state)
        {
            if (state == playing) reached.TrySetResult();
        }

        PlaybackChanged += OnChanged;
        try
        {
            var finished = await Task.WhenAny(reached.Task, Task.Delay(timeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return finished == reached.Task;
        }
        finally
        {
            PlaybackChanged -= OnChanged;
        }
    }

    private void OnOscMessageReceived(object? sender, OscSubscriptionEvent e)
    {
        if (e.Message.Arguments.FirstOrDefault() is not bool value) return;

        switch (e.Message.Address)
        {
            case "/dolly/Play":
                IsPlaying = value;
                PlaybackChanged?.Invoke(this, value);
                StateChanged?.Invoke(this, EventArgs.Empty);
                break;
            case "/usercamera/LookAtMe":
                LookAtMe = value;
                break;
        }
    }

    public void Dispose()
    {
        _osc.OnOscMessageReceived -= OnOscMessageReceived;
    }
}
