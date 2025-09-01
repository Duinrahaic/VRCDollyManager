using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using VRCDollyManager.Client.ViewModels;
using VRCDollyManager.Client.Views;
using VRCDollyManager.Extensions;
using Application = Avalonia.Application;

namespace VRCDollyManager;

public class App : Application, IDisposable
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    public static IHost? AppHost { get; private set; }


    public void Dispose()
    {
        if (_desktop != null)
        {
            _desktop.Exit -= Exit;
            _desktop = null;
        }

        AppHost?.Dispose();
        AppHost = null;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _desktop.Exit += Exit;
            _desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            _desktop.MainWindow = new ClientWindow
            {
                DataContext = new ClientWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }


    private void Exit(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }


    internal static void RunAvaloniaAppWithHosting(string[] args, Func<AppBuilder> buildAvaloniaApp)
    {
        var appBuilder = Host.CreateApplicationBuilder(args);
        appBuilder.Services.AddWindowsFormsBlazorWebView();
        appBuilder.Services.AddBlazorWebViewDeveloperTools();
 

        try
        {
  
            appBuilder.Logging
                .ClearProviders()
                .AddSerilog( new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext()
                .CreateLogger());   
            appBuilder.Services.RegisterServices();
            AppHost = appBuilder.BuildApp();
            AppHost.Start();
            buildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "VDM Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Console.WriteLine(ex);
            Environment.Exit(0);
        }
    }
}