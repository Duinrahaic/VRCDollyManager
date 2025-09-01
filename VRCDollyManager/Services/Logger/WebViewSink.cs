using Microsoft.Web.WebView2.Core;
using Serilog.Core;
using Serilog.Events;

namespace VRCDollyManager.Services.Logger;

public class AppConsoleLogger(CoreWebView2 webView) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {

        string level = logEvent.Level switch
        {
            LogEventLevel.Verbose => "debug",
            LogEventLevel.Debug => "debug",
            LogEventLevel.Information => "info",
            LogEventLevel.Warning => "warn",
            LogEventLevel.Error => "error",
            LogEventLevel.Fatal => "error",
            _ => "log"
        };

        string message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
            message += $" | Exception: {logEvent.Exception}";

        var escaped = message.Replace("\\", "\\\\").Replace("\"", "\\\"");

        string script = $"console.{level}(\"[Serilog] {escaped}\");";

        // fire-and-forget (don’t block logging pipeline)
        _ = webView.ExecuteScriptAsync(script);
    }
}