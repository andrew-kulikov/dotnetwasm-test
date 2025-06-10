using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Cryptography;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

Log.Info("Hello World");

public struct ImageResult
{
    public int Width;
    public int Height;
}

public static class Log
{
    public static void Info(string message)
    {
        Console.WriteLine($"DOTNET | {message}");
    }
}

// Make the program wait until manually terminated
public partial class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    [UnmanagedCallersOnly(EntryPoint = "Test_SayHello")]
    internal static unsafe void SayHello()
    {
        Log.Info($"Hello from dotnet");
    }

    [JSImport("globalThis.loadImageAsync")]
    internal static partial Task<string> LoadJsImageAsync(string imageUrl);

    [JSImport("globalThis.decodeLoadedImage")]
    internal static partial void DecodeJsImage(string imageUrl, IntPtr bufferPtr, int bufferSize);

    [JSExport]
    internal static void GetSum(string data)
    {
        var sw = Stopwatch.StartNew();
        Log.Info(data);
        // Using source generator for better performance and trimming support
        var d = JsonConvert.DeserializeObject<DataToSum>(data);
        if (d != null)
        {
            sw.Stop();
            Log.Info($"{d.A + d.B}");
            Log.Info($"Deserialization took {sw}");
        }
    }

    [JSExport]
    internal static async Task DecodeBrowserWebPViaDotNet(string imageUrl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var imageResultStr = await LoadJsImageAsync(imageUrl);
            var imageResult = JsonConvert.DeserializeObject<ImageResult>(imageResultStr);
            Log.Info($"Image loaded: {imageResult.Width}x{imageResult.Height}");

            var bufferSize = imageResult.Width * imageResult.Height * 4; // RGBA
            var bufferPtr = Marshal.AllocHGlobal(bufferSize);
            Span<byte> bufferSpan;
            unsafe
            {
                bufferSpan = new Span<byte>(bufferPtr.ToPointer(), bufferSize);
            }
            CryptographicOperations.ZeroMemory(bufferSpan); // Clear the buffer
            Log.Info($"Before: {bufferSpan[0]:X2} {bufferSpan[1]:X2} {bufferSpan[2]:X2} {bufferSpan[3]:X2}");

            DecodeJsImage(imageUrl, bufferPtr, bufferSize);
            Log.Info($"After:  {bufferSpan[0]:X2} {bufferSpan[1]:X2} {bufferSpan[2]:X2} {bufferSpan[3]:X2}");

            Marshal.FreeHGlobal(bufferPtr);
            sw.Stop();

            Log.Info($"Browser decode via .NET completed in {sw.Elapsed}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Info($"Browser decode via .NET failed: {ex.Message}");
        }
    }

    [JSExport]
    internal static async Task DecodeWebPWithDotNet(string imageUrl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Log.Info($"Starting .NET WASM decode of: {imageUrl}");

            // Download the WebP image
            var downloadStart = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            downloadStart.Stop();

            Log.Info($"Downloaded {imageBytes.Length} bytes in {downloadStart.ElapsedMilliseconds}ms");

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

            Log.Info($".NET WASM decode completed in {totalTime}ms (download: {downloadStart.ElapsedMilliseconds}ms, processing: {processingStart.ElapsedMilliseconds}ms)");
            Log.Info($"Image decoded: {width}x{height} pixels ({pixelCount} total pixels, {pixelData.Length * 4} RGBA bytes)");

        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Info($".NET WASM decode failed: {ex.Message}");
        }
    }

    [JSExport]
    internal static async Task DecodeWebPWithLibwebp(string imageUrl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Download the WebP image
            var downloadStart = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            downloadStart.Stop();

            var processingStart = Stopwatch.StartNew();
            WebPDecBuffer decBuffer;
            unsafe
            {
                int width = 0, height = 0;

                fixed (byte* pointer = &bytes[0])
                {
                    WebPNative.Instance.GetInfo(new IntPtr(pointer), (UIntPtr)bytes.Length, out width, out height);

                    WebPDecoderConfig config = new WebPDecoderConfig();

                    var status = WebPNative.Instance.Decode(new IntPtr(pointer), (UIntPtr)bytes.Length, &config);
                    if (status != VP8StatusCode.VP8_STATUS_OK)
                        throw new Exception("Error during decoding WebP: " + status);

                    decBuffer = config.output;
                }

                processingStart.Stop();
                sw.Stop();
                var totalTime = sw.ElapsedMilliseconds;

                Log.Info($".NET WASM decode completed in {sw.Elapsed} (download: {downloadStart.Elapsed}, processing: {processingStart.Elapsed})");
                Log.Info($"Image decoded: {width}x{height} pixels {decBuffer.data.RGBA.size}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Info($".NET WASM decode failed: {ex.Message}");
        }
    }
}

public record DataToSum(int A, int B);
