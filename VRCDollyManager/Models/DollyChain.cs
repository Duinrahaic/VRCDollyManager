namespace VRCDollyManager.Models;

/// <summary>
/// A named, ordered list of dolly files that are played back-to-back.
/// </summary>
public class DollyChain
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Dolly file names (as stored in <see cref="Dolly.Name"/>), in playback order.</summary>
    public List<string> Dollies { get; set; } = new();

    /// <summary>Turn on the camera's Look-At-Me while the chain plays, keeping it aimed at the player's face.</summary>
    public bool LookAtMe { get; set; }
}
