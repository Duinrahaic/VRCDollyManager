using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using VRCDollyManager.Extensions;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public interface IKeypressWatcherService
{
    event EventHandler? OnKeyPress;
    void RegisterHotkey(Keys key, uint modifiers, bool save = true);
    void LoadHotkey(Action callback);
    void UnregisterHotkey();
    void Enable();
    void Disable();
    bool IsHotkeySet();
    bool IsEnabled();
    string GetHotkey();
    void Dispose();
}

public class KeypressWatcherService : IKeypressWatcherService
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _proc;
    private IntPtr _hook = IntPtr.Zero;

    private readonly ILogger<KeypressWatcherService> _logger;
    private readonly string _configPath;

    private Keys _registeredKey = Keys.None;
    private uint _registeredModifiers = 0;
    private bool _enabled = false;  

    public event EventHandler? OnKeyPress;

    public KeypressWatcherService(ILogger<KeypressWatcherService> logger)
    {
        _logger = logger;
        _configPath = DollyManagerFilePaths.GetVDMConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Failed to set keyboard hook: " + Marshal.GetLastWin32Error());

        _logger.LogInformation("KeypressWatcherService initialized and hook active");
        LoadHotkey(() => { }); // Load config on startup
    }

    public void RegisterHotkey(Keys key, uint modifiers, bool save = true)
    {
        _registeredKey = key;
        _registeredModifiers = modifiers;

        _logger.LogInformation($"Registered hotkey {modifiers}+{key}");

        if (save)
        {
            SaveConfig(new HotkeyConfig { Key = key, Modifiers = modifiers, Enabled = _enabled });
        }
    }

    public void LoadHotkey(Action callback)
    {
        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<HotkeyConfig>(json);

            if (config != null)
            {
                _registeredKey = config.Key;
                _registeredModifiers = config.Modifiers;
                _enabled = config.Enabled; // NEW load enabled state
                _logger.LogInformation($"Loaded hotkey {config.Modifiers}+{config.Key}, Enabled={_enabled}");

                OnKeyPress += (_, __) => callback();
            }
        }
    }

    public void UnregisterHotkey()
    {
        _logger.LogInformation("Unregistering hotkey");
        _registeredKey = Keys.None;
        _registeredModifiers = 0;
        SaveConfig(new HotkeyConfig { Key = Keys.None, Modifiers = 0, Enabled = _enabled });
    }

    public void Enable()
    {
        _enabled = true;
        _logger.LogInformation("Hotkey service enabled");
        SaveConfig(new HotkeyConfig { Key = _registeredKey, Modifiers = _registeredModifiers, Enabled = _enabled });
    }
    
 
    public void Disable()
    {
        _enabled = false;
        _logger.LogInformation("Hotkey service disabled");
        SaveConfig(new HotkeyConfig { Key = _registeredKey, Modifiers = _registeredModifiers, Enabled = _enabled });
    }

    public bool IsHotkeySet() => _registeredKey != Keys.None;
    public bool IsEnabled() => _enabled;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void SaveConfig(HotkeyConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (_enabled &&
            nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if (IsHotkeyMatch((Keys)data.vkCode))
            {
                _logger.LogInformation("Hotkey pressed");
                OnKeyPress?.Invoke(this, EventArgs.Empty);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool IsHotkeyMatch(Keys key)
    {
        if (_registeredKey == Keys.None) return false;

        bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
        bool alt = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;
        bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
        bool win = (Control.ModifierKeys & Keys.LWin) == Keys.LWin ||
                   (Control.ModifierKeys & Keys.RWin) == Keys.RWin;

        if ((_registeredModifiers & 0x0002) != 0 && !ctrl) return false;
        if ((_registeredModifiers & 0x0001) != 0 && !alt) return false;
        if ((_registeredModifiers & 0x0004) != 0 && !shift) return false;
        if ((_registeredModifiers & 0x0008) != 0 && !win) return false;

        return key == _registeredKey;
    }
    
    public const uint MOD_NONE    = 0x0000;
    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;
    public const uint MOD_WIN     = 0x0008;
    
    public string GetHotkey()
    {
        if (_registeredKey == Keys.None)
            return "Unbound";

        var parts = new List<string>();

        if ((_registeredModifiers & MOD_CONTROL) != 0)
            parts.Add("Ctrl");
        if ((_registeredModifiers & MOD_ALT) != 0)
            parts.Add("Alt");
        if ((_registeredModifiers & MOD_SHIFT) != 0)
            parts.Add("Shift");
        if ((_registeredModifiers & MOD_WIN) != 0)
            parts.Add("Win");

        parts.Add(_registeredKey.ToString());
        if (parts.Count == 1)
        {
            return parts[0];
        }
        return string.Join("+", parts);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
