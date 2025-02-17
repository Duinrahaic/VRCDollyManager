using System.Text.Json;
using VRCDollyManager.Models;

namespace VRCDollyManager.Extensions;

public static class DollyExtensions
{
    public static string GetDollyPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VRChat", "CameraPaths");
    }
    
    public static string GetDollyFilePath(this Dolly dolly)
    {
        return Path.Combine(GetDollyPath(), dolly.Name);
    }

    public static string GetDollyDbPath()
    {
        return Path.Combine(GetDollyPath(), "vdm_database.sqlite");
    }

    public static List<CameraKeyFrame>? GetCameraKeyFrames(this Dolly dolly)
    {
        var keyFramePath = Path.Combine(GetDollyPath(), $"{dolly.Name}");
        if (!File.Exists(keyFramePath))
            return new List<CameraKeyFrame>();
        return JsonSerializer.Deserialize<List<CameraKeyFrame>>(File.ReadAllText(keyFramePath));
    }
    
    public static string GetCameraKeyFramesToString(this Dolly dolly, bool isLocal = false)
    {
        var keyFramePath = Path.Combine(GetDollyPath(), $"{dolly.Name}");
        if (!File.Exists(keyFramePath))
            return string.Empty;
        return File.ReadAllText(keyFramePath);
    }
}