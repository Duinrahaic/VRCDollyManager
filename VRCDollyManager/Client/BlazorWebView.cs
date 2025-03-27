using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Web.WebView2.Core;

namespace VRCDollyManager.Client;

public class BlazorWebView : NativeControlHost
{
    /// <summary>
    ///     The <see cref="AvaloniaProperty" /> which backs the <see cref="ZoomFactor" /> property.
    /// </summary>
    public static readonly DirectProperty<BlazorWebView, double> ZoomFactorProperty
        = AvaloniaProperty.RegisterDirect<BlazorWebView, double>(
            nameof(ZoomFactor),
            x => x.ZoomFactor,
            (x, y) => x.ZoomFactor = y);

    public static readonly DirectProperty<BlazorWebView, IServiceProvider> ServicesProperty
        = AvaloniaProperty.RegisterDirect<BlazorWebView, IServiceProvider>(
            nameof(Services),
            x => x.Services,
            (x, y) => x.Services = y);

    public static readonly DirectProperty<BlazorWebView, RootComponentsCollection> RootComponentsProperty
        = AvaloniaProperty.RegisterDirect<BlazorWebView, RootComponentsCollection>(
            nameof(RootComponents),
            x => x.RootComponents,
            (x, y) => x.RootComponents = y);

    private Microsoft.AspNetCore.Components.WebView.WindowsForms.BlazorWebView? _blazorWebView;
    private string? _hostPage;
    private IServiceProvider _serviceProvider = default!;
    private Uri? _source;
    private double _zoomFactor = 1.0;


    public string? HostPage
    {
        get
        {
            if (_blazorWebView != null) _hostPage = _blazorWebView.HostPage;
            return _hostPage;
        }

        set
        {
            if (_hostPage != value)
            {
                _hostPage = value;
                if (_blazorWebView != null) _blazorWebView.HostPage = value;
            }
        }
    }

    public Uri? Source
    {
        get
        {
            if (_blazorWebView != null) _source = _blazorWebView.WebView.Source;
            return _source;
        }

        set
        {
            if (_source != value)
            {
                _source = value;
                if (_blazorWebView != null) _blazorWebView.WebView.Source = value;
            }
        }
    }

    public double ZoomFactor
    {
        get
        {
            if (_blazorWebView != null) _zoomFactor = _blazorWebView.WebView.ZoomFactor;
            return _zoomFactor;
        }

        set
        {
            if (_zoomFactor != value)
            {
                _zoomFactor = value;
                if (_blazorWebView != null) _blazorWebView.WebView.ZoomFactor = value;
            }
        }
    }

    public IServiceProvider Services
    {
        get => _serviceProvider;
        set
        {
            _serviceProvider = value;
            if (_blazorWebView != null) _blazorWebView.Services = _serviceProvider;
        }
    }

    public RootComponentsCollection RootComponents { get; set; } = new();

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (OperatingSystem.IsWindows())
        {
            // Initialize the Blazor WebView
            _blazorWebView = new Microsoft.AspNetCore.Components.WebView.WindowsForms.BlazorWebView
            {
                HostPage = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                    "wwwroot\\index.html"),
                Services = _serviceProvider,
                BlazorWebViewInitializing = (sender, e) =>
                {
                    e.EnvironmentOptions = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = "--enable-experimental-web-platform-features",


                    };
                }
            };
            
            _blazorWebView.WebView.CoreWebView2InitializationCompleted+= (sender, args) =>
            {
                _blazorWebView.WebView.CoreWebView2.IsDefaultDownloadDialogOpenChanged += (sender, e) =>
                {
                    if (_blazorWebView.WebView.CoreWebView2.IsDefaultDownloadDialogOpen)
                        _blazorWebView.WebView.CoreWebView2.CloseDefaultDownloadDialog();
                };
            };

        foreach (var component in RootComponents) _blazorWebView.RootComponents.Add(component);

            return new PlatformHandle(_blazorWebView.Handle, "HWND");
        }

        return base.CreateNativeControlCore(parent);
    }


    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (OperatingSystem.IsWindows())
        {
            _blazorWebView?.Dispose();
            _blazorWebView = null;
        }
        else
        {
            base.DestroyNativeControlCore(control);
        }
    }


    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            _blazorWebView?.Dispose();
            _blazorWebView = null;
        }

        base.OnUnloaded(e);
    }
}