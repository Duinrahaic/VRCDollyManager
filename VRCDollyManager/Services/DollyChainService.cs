using System.Text.Json;
using VRCDollyManager.Extensions;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

/// <summary>
/// Stores dolly chains as one JSON file per chain in the DollyManager Chains folder.
/// </summary>
public class DollyChainService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Saved chain names, most recently saved first.</summary>
    public List<string> GetChainNames()
    {
        var folder = DollyManagerFilePaths.GetChainsFolder();
        if (!Directory.Exists(folder)) return new List<string>();

        return new DirectoryInfo(folder).GetFiles("*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => Load(f.FullName)?.Name)
            .OfType<string>()
            .ToList();
    }

    public DollyChain? GetChain(string name)
    {
        var path = GetChainPath(name);
        return File.Exists(path) ? Load(path) : null;
    }

    public void SaveChain(DollyChain chain)
    {
        DollyManagerFilePaths.CreateFolder(DollyManagerFilePaths.GetChainsFolder());
        File.WriteAllText(GetChainPath(chain.Name), JsonSerializer.Serialize(chain, JsonOptions));
    }

    private static DollyChain? Load(string path) =>
        JsonSerializer.Deserialize<DollyChain>(File.ReadAllText(path));

    private static string GetChainPath(string name)
    {
        var fileName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(DollyManagerFilePaths.GetChainsFolder(), $"{fileName}.json");
    }
}
