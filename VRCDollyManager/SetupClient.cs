using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.ReactiveUI;

namespace VRCDollyManager;

public static class SetupClient
{
    public static void Start(string[] args)
    {
        try
        {
            App.RunAvaloniaAppWithHosting(args, BuildAvaloniaApp); // Builds WebView
        }
        catch (Exception ex)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }


    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();
    }
}