using System.Text.Json;
using VRCDollyManager.Models;

namespace VRCDollyManager.Extensions;

public static class DollyExtensions
{
    public static void OpenDollyFolder()
    {
        var path = GetDollyPath();
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
    
    public static string GetDollyPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VRChat", "CameraPaths");
    }
    public static string GetTempDollyPath()
    {
        return Path.Combine(GetDollyPath(),"Temp");
    }
    
    public static string GetDollyFilePath(this Dolly dolly)
    {
        return Path.Combine(GetDollyPath(), dolly.Name);
    }
    public static string? GetRelativeDollyKeyframes(this Dolly dolly)
    {
        var data = dolly.GetCameraKeyFrames();
        if (data == null)
            return null;
        foreach (var frame in data)
        {
            frame.IsLocal = true;
        }

        try
        {
            // if folder exists make it 
            if (!Directory.Exists(GetTempDollyPath()))
                Directory.CreateDirectory(GetTempDollyPath());
            
            
            var json = JsonSerializer.Serialize(data);
            // write all lines to file and return the path
            var path = Path.Combine(GetTempDollyPath(), $"modified_{dolly.Name}");
            File.WriteAllText(path, json);
            return path;
        }catch(Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
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