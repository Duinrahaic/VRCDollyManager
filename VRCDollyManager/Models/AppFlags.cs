namespace VRCDollyManager.Models;

public static class AppFlags
{
    public static bool IsVRMode { get; private set; }

    public static void CheckArgs(string[] args)
    {
        if (args.Contains("-vr"))
        {
            IsVRMode = true;
        }
        
    }
}