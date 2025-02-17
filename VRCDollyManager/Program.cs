using System.Diagnostics;

namespace VRCDollyManager;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var globalMutex = new Mutex(true, @"Local\VDM.exe", out var mutexSuccess);
        if (!mutexSuccess)
        {
            Debug.Print("App is already running. Quitting...");
            globalMutex.Close();
            return;
        }

        SetupClient.Start(args);
        globalMutex.Close();
    }
}