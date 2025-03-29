using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Forms;

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
    public static void CopyToClipboard(string fileName, string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentNullException("File name or content cannot be null or empty.");

        // Define the path to the VRChat folder in MyDocuments
        string vrChatPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VRChat");

        // Ensure the VRChat directory exists
        if (!Directory.Exists(vrChatPath))
        {
            Directory.CreateDirectory(vrChatPath);
        }

        // Full file path within the VRChat directory
        string filePath = Path.Combine(vrChatPath, fileName);

        // Write the JSON content to the file (overwrite if it exists)
        File.WriteAllText(filePath, jsonContent);

        // Prepare a file drop list
        var fileDropList = new StringCollection();
        fileDropList.Add(filePath);

        // Copy the file to the clipboard
        Clipboard.SetFileDropList(fileDropList);

        Console.WriteLine($"File saved to {filePath} and copied to clipboard.");
    }
}