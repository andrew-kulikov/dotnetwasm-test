// Import required runtime
import { dotnet } from './_framework/dotnet.js'

// Initialize the .NET runtime
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withDiagnosticTracing(false)
    .create();

// Get exports from our assembly
const config = getConfig();
console.log('Config:', config);
const exports = await getAssemblyExports(config.mainAssemblyName);

console.log('WASM module loaded successfully!');

function getRandomInt(max) {
    return Math.floor(Math.random() * max);
}

// Browser-side WebP decoding
async function decodeBrowserWebP(imageUrl) {
    const startTime = performance.now();
    
    try {
        console.log(`Starting browser decode of: ${imageUrl}`);
        
        // Create virtual image element
        const img = new Image();
        img.crossOrigin = "anonymous"; // Enable CORS if needed
        
        // Wait for image to load
        await new Promise((resolve, reject) => {
            img.onload = resolve;
            img.onerror = reject;
            img.src = imageUrl;
        });
        
        console.log(`Image loaded: ${img.width}x${img.height}`);
        
        // Create virtual canvas
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        canvas.width = img.width;
        canvas.height = img.height;
        
        // Draw image to canvas
        ctx.drawImage(img, 0, 0);
        
        // Get image data (RGBA bytes)
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const bytes = imageData.data;
        
        const endTime = performance.now();
        const duration = endTime - startTime;
        
        console.log(`Browser decode completed in ${duration.toFixed(2)}ms`);
        console.log(`Image data size: ${bytes.length} bytes (${canvas.width}x${canvas.height}x4)`);
        
        document.getElementById('browserResult').innerHTML = 
            `<strong>Browser Runtime:</strong> ${duration.toFixed(2)}ms - ${canvas.width}x${canvas.height} - ${bytes.length} bytes`;
        
        return {
            width: canvas.width,
            height: canvas.height,
            duration: duration,
            dataSize: bytes.length
        };
        
    } catch (error) {
        console.error('Browser decode failed:', error);
        document.getElementById('browserResult').innerHTML = 
            `<strong>Browser Runtime:</strong> Failed - ${error.message}`;
        throw error;
    }
}

// Set up event handlers
document.getElementById("helloBtn").onclick = function () {
    var a = getRandomInt(100);
    var b = getRandomInt(100);
    console.log(`Calling .NET with ${a} and ${b}`);
    exports.Program.GetSum(JSON.stringify({ A: a, B: b }));
}

document.getElementById("browserDecodeBtn").onclick = async function () {
    const url = document.getElementById("webpUrl").value;
    if (!url) {
        alert("Please enter a WebP image URL");
        return;
    }
    await decodeBrowserWebP(url);
}

document.getElementById("wasmDecodeBtn").onclick = function () {
    const url = document.getElementById("webpUrl").value;
    if (!url) {
        alert("Please enter a WebP image URL");
        return;
    }
    console.log(`Calling .NET WASM decoder with: ${url}`);
    exports.Program.DecodeWebPWithDotNet(url);
}

// Helper function for WASM to update UI
window.updateWasmResultFromDotNet = function(result) {
    document.getElementById('wasmResult').innerHTML = 
        `<strong>.NET WASM Runtime:</strong> ${result}`;
}

await runMain();
