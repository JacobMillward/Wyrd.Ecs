using System.Runtime.InteropServices;
using SDL3;

// args[0] = source shader directory, args[1] = output directory.
// Pipeline is HLSL -> SPIR-V -> {DXIL, MSL}, not three independent HLSL compiles: SDL3-CS's
// ShaderCross only exposes a convenience string-based overload for CompileSPIRVFromHLSL;
// CompileDXILFromHLSL/TranspileMSLFromSPIRV take a raw SPIRVInfo struct (native pointers),
// so reusing the SPIR-V bytecode already in hand needs one native marshal, not two.
var sourceDir = args[0];
var outputDir = args[1];
Directory.CreateDirectory(outputDir);

if (!ShaderCross.Init())
    throw new InvalidOperationException($"SDL_ShaderCross_Init failed: {SDL.GetError()}");

try
{
    var shaders = new[]
    {
        ("UnlitSprite.vert", ShaderCross.ShaderStage.Vertex),
        ("UnlitSprite.frag", ShaderCross.ShaderStage.Fragment),
    };

    foreach (var (name, stage) in shaders)
    {
        var hlslPath = Path.Combine(sourceDir, $"{name}.hlsl");
        var hlslSource = File.ReadAllText(hlslPath);

        var spirvPtr = ShaderCross.CompileSPIRVFromHLSL(hlslSource, "main", stage, out var spirvSize, includeDir: null!, props: 0);
        if (spirvPtr == IntPtr.Zero)
            throw new InvalidOperationException($"CompileSPIRVFromHLSL failed for {name}: {SDL.GetError()}");
        var spirvBytes = new byte[(int)spirvSize];
        Marshal.Copy(spirvPtr, spirvBytes, 0, spirvBytes.Length);
        SDL.Free(spirvPtr);
        File.WriteAllBytes(Path.Combine(outputDir, $"{name}.spirv"), spirvBytes);

        var entrypointPtr = Marshal.StringToHGlobalAnsi("main");
        try
        {
            unsafe
            {
                fixed (byte* spirvPin = spirvBytes)
                {
                    var spirvInfo = new ShaderCross.SPIRVInfo
                    {
                        ByteCode = (IntPtr)spirvPin,
                        ByteCodeSize = (UIntPtr)spirvBytes.Length,
                        Entrypoint = entrypointPtr,
                        ShaderStage = stage,
                        Props = 0,
                    };

                    var dxilPtr = ShaderCross.CompileDXILFromSPIRV(in spirvInfo, out var dxilSize);
                    if (dxilPtr == IntPtr.Zero)
                        throw new InvalidOperationException($"CompileDXILFromSPIRV failed for {name}: {SDL.GetError()}");
                    var dxilBytes = new byte[(int)dxilSize];
                    Marshal.Copy(dxilPtr, dxilBytes, 0, dxilBytes.Length);
                    SDL.Free(dxilPtr);
                    File.WriteAllBytes(Path.Combine(outputDir, $"{name}.dxil"), dxilBytes);

                    var mslPtr = ShaderCross.TranspileMSLFromSPIRV(in spirvInfo);
                    if (mslPtr == IntPtr.Zero)
                        throw new InvalidOperationException($"TranspileMSLFromSPIRV failed for {name}: {SDL.GetError()}");
                    var mslText = Marshal.PtrToStringUTF8(mslPtr) ?? throw new InvalidOperationException("TranspileMSLFromSPIRV returned null text");
                    SDL.Free(mslPtr);
                    File.WriteAllText(Path.Combine(outputDir, $"{name}.msl"), mslText);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(entrypointPtr);
        }
    }

    Console.WriteLine($"OK: compiled {shaders.Length} shaders to {outputDir}");
}
finally
{
    ShaderCross.Quit();
}

