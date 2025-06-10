// Import required runtime
import { dotnet } from './_framework/dotnet.js'

globalThis.cachedImages = {};
globalThis.canvas2 = document.createElement('canvas');

// Helper function called by .NET to perform actual browser image decoding
// Define this BEFORE initializing .NET runtime
globalThis.loadImageAsync = async function (imageUrl) {
    const startTime = performance.now();

    console.log(`JS | Loading image ${imageUrl}`);

    // Create virtual image element
    const img = new Image();
    img.crossOrigin = "anonymous"; // Enable CORS if needed

    // Wait for image to load
    await new Promise((resolve, reject) => {
        img.onload = () => {
            globalThis.cachedImages[imageUrl] = img;
            resolve();
        }
        img.onerror = reject;
        img.src = imageUrl;
    });

    const endTime = performance.now();
    const duration = endTime - startTime;

    console.log(`JS | Loading completed in ${duration.toFixed(2)}ms`);

    return JSON.stringify({
        width: img.width,
        height: img.height,
    });
}

globalThis.decodeLoadedImage = function (imageUrl, bufferPtr, bufferLength) {
    const startTime = performance.now();

    console.log(`JS | Decoding ${imageUrl}`);

    const img = globalThis.cachedImages[imageUrl];
    if (!img) {
        throw new Error(`Image not found in cache: ${imageUrl}`);
    }

    // Create virtual canvas
    const canvas = globalThis.canvas2;
    const ctx = canvas.getContext('2d');
    canvas.width = img.width;
    canvas.height = img.height;

    // Draw image to canvas
    ctx.drawImage(img, 0, 0);

    // Get image data (RGBA bytes)
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const bytes = imageData.data;

    if (bytes.length !== bufferLength) {
        throw new Error(`Buffer length mismatch: expected ${bufferLength}, got ${bytes.length}`);
    }

    Module.HEAPU8.set(bytes, bufferPtr);

    const endTime = performance.now();
    const duration = endTime - startTime;

    console.log(`JS | Decode completed in ${duration.toFixed(2)}ms`);
}


// Initialize the .NET runtime
const { getAssemblyExports, getConfig, runMain, Module } = await dotnet
    .withDiagnosticTracing(false)
    .create();

// Get exports from our assembly
const config = getConfig();
console.log('Config:', config);
const exports = await getAssemblyExports(config.mainAssemblyName);

globalThis.Module = Module;

console.log('WASM module loaded successfully!');

function getRandomInt(max) {
    return Math.floor(Math.random() * max);
}

// Browser-side WebP decoding (now calls into .NET first)
async function decodeBrowserWebP(imageUrl) {
    console.log(`Calling .NET to coordinate browser decode of: ${imageUrl}`);
    await exports.Program.DecodeBrowserWebPViaDotNet(imageUrl);
}

// Set up event handlers
document.getElementById("helloBtn").onclick = function () {
    var a = getRandomInt(100);
    var b = getRandomInt(100);
    console.log(`Calling .NET with ${a} and ${b}`);
    exports.Program.GetSum(JSON.stringify({ A: a, B: b }));
}

document.getElementById("browserDecodeBtn").onclick = async function () {
    const startTime = performance.now();
    const url = document.getElementById("webpUrl").value;
    if (!url) {
        alert("Please enter a WebP image URL");
        return;
    }
    await decodeBrowserWebP(url);
    const endTime = performance.now();
    const duration = endTime - startTime;

    document.getElementById('browserResult').innerHTML = `Browser result: ${duration}`;
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

document.getElementById("wasmLibwebpDecodeBtn").onclick = async function () {
    const startTime = performance.now();
    const url = document.getElementById("webpUrl").value;
    if (!url) {
        alert("Please enter a WebP image URL");
        return;
    }
    console.log(`Calling .NET WASM decoder with: ${url}`);
    await exports.Program.DecodeWebPWithLibwebp(url);
    const endTime = performance.now();
    const duration = endTime - startTime;

    document.getElementById('wasmResult').innerHTML = `libwebp result: ${duration}`;
}

// Helper function for WASM to update UI
window.updateWasmResultFromDotNet = function (result) {
    document.getElementById('wasmResult').innerHTML =
        `<strong>.NET WASM Runtime:</strong> ${result}`;
}

await runMain();
