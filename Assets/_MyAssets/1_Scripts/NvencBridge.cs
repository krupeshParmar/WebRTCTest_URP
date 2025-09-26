#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define NVENC_WINDOWS
#endif

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class NvencBridge : IDisposable
{
#if NVENC_WINDOWS
    public const string _Dll = "WebRTCStreamer.dll";

    // ---- C ABI ----
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_InitAutoD3D11(int width, int height, int fps, int bitrateKbps);

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_InitWithD3D11Device(IntPtr d3d11Device, int width, int height, int fps, int bitrateKbps);

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_ConnectWithWs(string signalingWsUrl, string roomOrPeerId);

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_PushFrame(IntPtr d3d11Texture2D, long pts100ns);

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_RequestKeyframe();

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_SetBitrateKbps(int bitrateKbps);

    [StructLayout(LayoutKind.Sequential)]
    public struct NwrStats { public ulong framesIn, framesOut, bytesOut; public double encodeMs, sendMs; }

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_GetStats(out NwrStats stats);

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_Shutdown();

    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)] 
    public static extern IntPtr NWR_GetD3D11Device();
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)] 
    public static extern bool NWR_InitVideoWithD3D11Device(IntPtr dev, int w, int h, int fps, int kbps);
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)] 
    public static extern IntPtr NWR_GetRenderEventFunc();
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)] 
    public static extern void NWR_CloseVideo();
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)] 
    public static extern void NWR_SetOutputDir(string path);

#else
    private static bool NWR_InitAutoD3D11(int w, int h, int fps, int kbps) => false;
    private static bool NWR_InitWithD3D11Device(IntPtr d, int w, int h, int fps, int kbps) => false;
    private static bool NWR_ConnectWithWs(string a, string b) => false;
    private static bool NWR_PushFrame(IntPtr p, long t) => false;
    private static void NWR_RequestKeyframe() { }
    private static void NWR_SetBitrateKbps(int k) { }
    private static bool NWR_GetStats(out NwrStats s) { s = default; return false; }
    private static void NWR_Shutdown() { }
    private static extern IntPtr NWR_GetD3D11Device() => nullptr;
    private static extern bool NWR_InitVideoWithD3D11Device(IntPtr dev, int w, int h, int fps, int kbps) => false;
    private static extern IntPtr NWR_GetRenderEventFunc() => nullptr;
    private static extern void NWR_CloseVideo() {}
    private static extern void NWR_SetOutputDir(string path) {}
#endif

    // ---- Managed state ----
    public bool IsInitialized { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Fps { get; private set; }
    public int BitrateKbps { get; private set; }

    /// <summary>Initialize using Unity’s current D3D11 device (recommended).</summary>
    public bool InitAuto(int width, int height, int fps, int bitrateKbps)
    {
        if (IsInitialized) Shutdown();
        Width = width; Height = height; Fps = fps; BitrateKbps = bitrateKbps;

        var ok = NWR_InitAutoD3D11(Width, Height, Fps, BitrateKbps);
        IsInitialized = ok;
        return ok;
    }

    /// <summary>Initialize with an explicit ID3D11Device* (advanced).</summary>
    public bool InitWithDevice(IntPtr d3d11Device, int width, int height, int fps, int bitrateKbps)
    {
        if (IsInitialized) Shutdown();
        Width = width; Height = height; Fps = fps; BitrateKbps = bitrateKbps;

        var ok = NWR_InitWithD3D11Device(d3d11Device, Width, Height, Fps, BitrateKbps);
        IsInitialized = ok;
        return ok;
    }

    /// <summary>Connect C++ rtc/libdatachannel peer via your Node.js signaling URL + room/peer id.</summary>
    public bool Connect(string signalingWsUrl, string roomOrPeerId)
        => NWR_ConnectWithWs(signalingWsUrl, roomOrPeerId);

    /// <summary>Send one frame.</summary>
    public bool Push(RenderTexture deterministicRT, double timeSeconds)
    {
        if (!IsInitialized || deterministicRT == null) return false;
        if (deterministicRT.width != Width || deterministicRT.height != Height)
        {
            // Not fatal, but useful to know.
            Debug.LogWarning($"NwrBridge: RT size {deterministicRT.width}x{deterministicRT.height} != encoder {Width}x{Height}.");
        }

        IntPtr tex = deterministicRT.GetNativeTexturePtr();    // ID3D11Texture2D*
        if (tex == IntPtr.Zero) return false;

        long pts100ns = (long)(timeSeconds * 10_000_000.0);    // 100ns PTS (monotonic)
        return NWR_PushFrame(tex, pts100ns);
    }

    /// <summary>Force an IDR keyframe (useful on viewer join/recover).</summary>
    public void ForceIdr() => NWR_RequestKeyframe();

    /// <summary>Change encoder bitrate on the fly (kbps).</summary>
    public void SetBitrate(int kbps)
    {
        BitrateKbps = kbps;
        NWR_SetBitrateKbps(kbps);
    }

    /// <summary>Fetch current stats from the native side.</summary>
    public bool TryGetStats(out ulong framesIn, out ulong framesOut, out ulong bytesOut, out double encodeMs, out double sendMs)
    {
        if (NWR_GetStats(out var s))
        {
            framesIn = s.framesIn; framesOut = s.framesOut; bytesOut = s.bytesOut;
            encodeMs = s.encodeMs; sendMs = s.sendMs;
            return true;
        }
        framesIn = framesOut = bytesOut = 0; encodeMs = sendMs = 0;
        return false;
    }

    public void Dispose() => Shutdown();

    public void Shutdown()
    {
        if (!IsInitialized) return;
        try { NWR_Shutdown(); } catch { /* ignore domain reload */ }
        IsInitialized = false;
    }
}
