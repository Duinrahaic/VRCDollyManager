using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace VRCDollyManager.Extensions;

public static class JSRuntimeExtensions
{
    public static async Task DownloadJsonAsync<T>(this IJSRuntime jsRuntime, string fileName, T obj)
    {
        if (obj == null)
        {
            Console.WriteLine("Object is null, nothing to download.");
            return;
        }

        // Serialize the object to JSON in memory
        string jsonContent = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });

        // Convert JSON to a Base64 string for download
        string base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));
        string dataUri = $"data:application/json;base64,{base64Data}";

        // Trigger download via JavaScript
        await jsRuntime.InvokeVoidAsync("triggerDownload", fileName, dataUri);
    }
}