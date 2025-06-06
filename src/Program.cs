﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

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
    internal static async Task DecodeBrowserWebPViaDotNet(string imageUrl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"Starting browser decode via .NET: {imageUrl}");

            // Call JavaScript to perform the actual browser-based decoding
            var result = await CallJavaScriptDecoder(imageUrl);

            sw.Stop();
            var totalTime = sw.ElapsedMilliseconds;

            Console.WriteLine($"Browser decode via .NET completed in {totalTime}ms");
            Console.WriteLine($"Result: {result}");

        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"Browser decode via .NET failed: {ex.Message}");
        }
    }

    [JSImport("globalThis.performBrowserImageDecode")]
    internal static partial Task<string> CallJavaScriptDecoder(string imageUrl);    [JSExport]
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

            // Actually decode the WebP image using ImageSharp
            var processingStart = Stopwatch.StartNew();

            using var memoryStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync<Rgba32>(memoryStream);
            
            // Get image information
            var width = image.Width;
            var height = image.Height;
            var pixelCount = width * height;
            
            // Access pixel data to ensure full decoding
            var pixelData = new Rgba32[pixelCount];
            image.CopyPixelDataTo(pixelData);
            
            processingStart.Stop();

            sw.Stop();
            var totalTime = sw.ElapsedMilliseconds;

            Console.WriteLine($".NET WASM decode completed in {totalTime}ms (download: {downloadStart.ElapsedMilliseconds}ms, processing: {processingStart.ElapsedMilliseconds}ms)");
            Console.WriteLine($"Image decoded: {width}x{height} pixels ({pixelCount} total pixels, {pixelData.Length * 4} RGBA bytes)");

        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($".NET WASM decode failed: {ex.Message}");
        }
    }
}

public record DataToSum(int A, int B);
