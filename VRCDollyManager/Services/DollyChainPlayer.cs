using VRCDollyManager.Extensions;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public enum ChainLoopMode
{
    None,
    All,
    One
}

/// <summary>
/// Plays a list of dollies back-to-back: imports each one, starts it, and moves on when VRChat
/// reports that playback stopped. Only the first dolly waits for the player's delay.
/// </summary>
public class DollyChainPlayer
{
    // VRChat ignores /dolly/Import while playing, and needs a moment to apply an import before playing.
    private static readonly TimeSpan ImportSettle = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    // VRChat doesn't document a stop report when a path finishes on its own, so a dolly also ends
    // once its keyframe timing has elapsed plus this margin.
    private static readonly TimeSpan EndMargin = TimeSpan.FromSeconds(5);

    private readonly DollyPlayerService _player;
    private readonly ILogger<DollyChainPlayer> _logger;
    private readonly object _lock = new();
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _dollyCts;
    private Task _run = Task.CompletedTask;
    private int? _jumpTo;
    private int _position;
    private bool _lookAtMe;
    private bool? _lookAtMeBefore;

    public event EventHandler? StateChanged;

    public DollyChainPlayer(DollyPlayerService player, ILogger<DollyChainPlayer> logger)
    {
        _player = player;
        _logger = logger;
    }

    public bool IsRunning => _runCts != null;

    /// <summary>The chain entry currently playing, or null when stopped.</summary>
    public Guid? CurrentId { get; private set; }

    public ChainLoopMode Loop { get; set; }

    /// <summary>Shuffle the order each time the chain starts or loops.</summary>
    public bool Shuffle { get; set; }

    /// <summary>
    /// Keep the camera aimed at the player's face with Look-At-Me while the chain runs.
    /// The camera's previous setting is restored when the chain stops.
    /// </summary>
    public bool LookAtMe
    {
        get => _lookAtMe;
        set
        {
            _lookAtMe = value;
            if (IsRunning) ApplyLookAtMe();
        }
    }

    public async Task StartAsync(IReadOnlyList<DirectorDolly> chain, int startIndex)
    {
        if (chain.Count == 0) return;
        Stop();
        await _run; // its final stop must not land on the new run's first dolly

        var order = chain.ToList();
        var first = order[Math.Clamp(startIndex, 0, order.Count - 1)];
        if (Shuffle)
        {
            order = Shuffled(order.Where(d => d != first)).Prepend(first).ToList();
        }

        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _runCts = cts;
            _jumpTo = null;
        }

        if (LookAtMe) ApplyLookAtMe();
        _run = Task.Run(() => RunAsync(order, order.IndexOf(first), cts));
    }

    public void Stop()
    {
        lock (_lock)
        {
            _runCts?.Cancel();
        }
    }

    public void Next() => Jump(+1);

    public void Previous() => Jump(-1);

    private void Jump(int offset)
    {
        lock (_lock)
        {
            if (_runCts == null) return;
            _jumpTo = _position + offset;
            _dollyCts?.Cancel();
        }
    }

    private async Task RunAsync(List<DirectorDolly> order, int position, CancellationTokenSource run)
    {
        var delayed = true;
        try
        {
            while (!run.IsCancellationRequested)
            {
                using var dollyCts = CancellationTokenSource.CreateLinkedTokenSource(run.Token);
                lock (_lock)
                {
                    _dollyCts = dollyCts;
                    _position = position;
                }

                CurrentId = order[position].Id;
                StateChanged?.Invoke(this, EventArgs.Empty);

                try
                {
                    if (!await PlayDollyAsync(order[position].Dolly, delayed, dollyCts.Token)) break;
                }
                catch (OperationCanceledException) when (!run.IsCancellationRequested)
                {
                    // Skipped: stop the current dolly so VRChat accepts the next import.
                    var stopped = _player.WaitForPlaybackAsync(false, StopTimeout, run.Token);
                    _player.Stop();
                    await stopped;
                }

                delayed = false;
                int next;
                lock (_lock)
                {
                    next = _jumpTo ?? (Loop == ChainLoopMode.One ? position : position + 1);
                    _jumpTo = null;
                }

                if (next >= order.Count && Loop == ChainLoopMode.All)
                {
                    next = 0;
                    if (Shuffle) order = Shuffled(order).ToList();
                }

                if (next < 0 || next >= order.Count) break;
                position = next;
            }
        }
        catch (OperationCanceledException)
        {
            _player.Stop();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Dolly chain playback failed");
            _player.Stop();
        }
        finally
        {
            lock (_lock)
            {
                _runCts = null;
                _dollyCts = null;
            }

            run.Dispose();
            if (_lookAtMeBefore is { } before)
            {
                _player.SetLookAtMe(before);
                _lookAtMeBefore = null;
            }

            CurrentId = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyLookAtMe()
    {
        _lookAtMeBefore ??= _player.LookAtMe ?? false;
        _player.SetLookAtMe(_lookAtMe);
    }

    /// <summary>Plays one dolly to the end. Returns false if VRChat never started it.</summary>
    private async Task<bool> PlayDollyAsync(Dolly dolly, bool delayed, CancellationToken cancellationToken)
    {
        await _player.ImportAsync(dolly);
        await Task.Delay(ImportSettle, cancellationToken);

        var delay = TimeSpan.FromSeconds(delayed ? _player.Delay : 0);
        var started = _player.WaitForPlaybackAsync(true, StartTimeout + delay, cancellationToken);
        if (delayed) _player.Play();
        else _player.PlayNow();

        if (!await started)
        {
            _logger.LogWarning("VRChat did not start {Dolly}; stopping the chain", dolly.Name);
            return false;
        }

        await _player.WaitForPlaybackAsync(false, delay + GetPlaybackLength(dolly) + EndMargin, cancellationToken);
        return true;
    }

    /// <summary>The longer of the dolly's Duration-mode and Speed-mode timing, since either mode may be active.</summary>
    private static TimeSpan GetPlaybackLength(Dolly dolly)
    {
        var keyFrames = dolly.GetCameraKeyFrames() ?? new List<CameraKeyFrame>();
        var byDuration = keyFrames
            .GroupBy(kf => kf.PathIndex)
            .Sum(path => path.OrderBy(kf => kf.Index).SkipLast(1).Sum(kf => kf.Duration));
        var bySpeed = keyFrames.CalculateTravelDurations().Values
            .SelectMany(path => path)
            .Select(kf => kf.TravelDuration)
            .Where(double.IsFinite)
            .Sum();
        return TimeSpan.FromSeconds(Math.Max(byDuration, bySpeed));
    }

    private static IEnumerable<DirectorDolly> Shuffled(IEnumerable<DirectorDolly> dollies) =>
        dollies.OrderBy(_ => Random.Shared.Next());
}
