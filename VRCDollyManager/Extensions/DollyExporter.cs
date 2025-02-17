namespace VRCDollyManager.Extensions;

using System.IO;
using System.Text.Json;
using Microsoft.JSInterop;

public class DollyExporter
{
    private readonly IJSRuntime _jsRuntime;

    public DollyExporter(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task ExportToClipboard()
    {
        string filePath = "wwwroot/dolly.json"; // Adjust if needed

        if (File.Exists(filePath))
        {
            string jsonContent = await File.ReadAllTextAsync(filePath);
            await _jsRuntime.InvokeVoidAsync("copyToClipboard", jsonContent);
        }
        else
        {
            Console.WriteLine("Dolly JSON file not found.");
        }
    }
}
