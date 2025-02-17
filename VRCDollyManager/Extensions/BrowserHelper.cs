using System.Diagnostics;

namespace VRCDollyManager.Extensions;

public static class BrowserHelper
{
    /// <summary>
    /// Opens the specified URL in the default web browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening URL: {ex.Message}");
        }
    }
}