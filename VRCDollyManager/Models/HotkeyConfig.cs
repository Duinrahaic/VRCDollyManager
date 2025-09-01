using System.Windows.Forms;

namespace VRCDollyManager.Models;

public class HotkeyConfig
{
    public Keys Key { get; set; }
    public uint Modifiers { get; set; }
    public bool Enabled { get; set; } = false;
}