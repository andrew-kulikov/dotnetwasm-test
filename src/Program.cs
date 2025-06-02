using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading.Tasks;

Console.WriteLine("Hello World");

// Create a source generator context for better AOT/trimming support
[JsonSerializable(typeof(DataToSum))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

// Make the program wait until manually terminated
public partial class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        // Allow flexible deserialization
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        TypeInfoResolver = SourceGenerationContext.Default
    };

    private static readonly HttpClient httpClient = new HttpClient();

    [UnmanagedCallersOnly(EntryPoint = "Test_SayHello")]
    internal static unsafe void SayHello()
    {
        Console.WriteLine($"Hello from dotnet");
    }

[JSExport]
    internal static void GetSum(string data)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine(data);
        // Using source generator for better performance and trimming support
        var d = JsonSerializer.Deserialize(data, SourceGenerationContext.Default.DataToSum);
        if (d != null)
        {
            sw.Stop();
            Console.WriteLine(d.A + d.B);
            Console.WriteLine($"Deserialization took {sw}");
        }
    }

    [JSExport]
    internal static async Task DecodeWebPWithDotNet(string imageUrl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"Starting .NET WASM decode of: {imageUrl}");
            
            // Download the WebP image
            var downloadStart = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            downloadStart.Stop();
            
            Console.WriteLine($"Downloaded {imageBytes.Length} bytes in {downloadStart.ElapsedMilliseconds}ms");
            
            // Simulate WebP decoding processing
            // In a real implementation, you would parse the WebP header and decode the image
            var processingStart = Stopwatch.StartNew();
            
            // Basic WebP header validation (simple check)
            bool isValidWebP = imageBytes.Length > 12 && 
                              imageBytes[0] == 'R' && imageBytes[1] == 'I' && 
                              imageBytes[2] == 'F' && imageBytes[3] == 'F' &&
                              imageBytes[8] == 'W' && imageBytes[9] == 'E' &&
                              imageBytes[10] == 'B' && imageBytes[11] == 'P';
            
            if (!isValidWebP)
            {
                throw new InvalidOperationException("Invalid WebP file format");
            }
            
            // Simulate decoding work
            await Task.Delay(50);
            processingStart.Stop();
            
            sw.Stop();
            var totalTime = sw.ElapsedMilliseconds;
            
            Console.WriteLine($".NET WASM decode completed in {totalTime}ms (download: {downloadStart.ElapsedMilliseconds}ms, processing: {processingStart.ElapsedMilliseconds}ms)");
            Console.WriteLine($"Image file size: {imageBytes.Length} bytes - Valid WebP: {isValidWebP}");
            
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($".NET WASM decode failed: {ex.Message}");
        }
    }
}

public record DataToSum(int A, int B);
