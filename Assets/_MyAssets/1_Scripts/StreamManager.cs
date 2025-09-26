using System.Runtime.InteropServices;
using System;
using UnityEngine;

using WebStream.DLLWrapper;
using UnityEngine.Rendering;
using System.Text;
public class StreamManager : MonoBehaviour
{
    public const string _Dll = "WebRTCStreamer.dll";

    // ---- C ABI ----
    // Initialize with d3d11 device
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_InitAutoD3D11(int width, int height, int fps, int bitrateKbps);

    // Initialize with d3d11 device
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_InitVideoWithD3D11Device(IntPtr d3d11Device, int width, int height, int fps, int bitrateKbps, bool saveLocally);

    // Manually connect with web socket
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_ConnectWithWs(string signalingWsUrl, string roomOrPeerId);

    //Manually push frame
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool NWR_PushFrame(IntPtr d3d11Texture2D, long pts100ns);

    // request nvenc to generate IDR frame
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Nvenc_RequestIDR();

    // Set nvenc bitrates
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_SetBitrateKbps(int bitrateKbps);

    // Shutdown Nvenc
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_Shutdown();

    // Get d3d11 device
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NWR_GetD3D11Device();

    // set pending pts
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_SetPendingPTS(long pts100ns);

    // Get Render Event function
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NWR_GetRenderEventFunc();

    // Set output directory for nvenc offline video saving
    [DllImport(_Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NWR_SetOutputDir(string path);

    public int width = 1920, height = 1080, fps = 60, bitrateKbps = 6000;
    public RenderTexture deterministicRT;
    public RenderTexture encodedRT;
    public Material yFlipMat;
    public bool saveLocally;

    CommandBuffer cb;
    long frameIndex = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        WebLoggerAPI.Start();
        if (deterministicRT == null) { Debug.LogError("Assign deterministicRT"); enabled = false; return; }

        NWR_SetOutputDir(Application.persistentDataPath + "/EncodedOut");


        IntPtr dev = NWR_GetD3D11Device();
        if (dev == IntPtr.Zero) { Debug.LogError("D3D11 device not available"); enabled = false; return; }
        if (!NWR_InitVideoWithD3D11Device(dev, width, height, fps, bitrateKbps, saveLocally))
        {
            Debug.LogError("NWR_InitVideoWithD3D11Device failed"); enabled = false; return;
        }
        Nvenc_RequestIDR();

        cb = new CommandBuffer { name = "NVENC Encode" };

        // Good runtime knobs
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
    }


    void OnDisable()
    {
        if (cb != null) { cb.Release(); cb = null; }
        if (yFlipMat) DestroyImmediate(yFlipMat);
        if (encodedRT) { encodedRT.Release(); DestroyImmediate(encodedRT); }
        WebLoggerAPI.StopSignaling();
    }


    void LateUpdate()
    {
        if (cb == null) return;
        if (!encodedRT || !yFlipMat) return;
        cb.Clear();

        cb.Blit(deterministicRT, encodedRT, yFlipMat);
        if (frameIndex % fps == 0) Nvenc_RequestIDR();
        long pts100ns = (long)System.Math.Round(frameIndex * (10_000_000.0 / fps));
        NWR_SetPendingPTS(pts100ns);
        frameIndex++;
        IntPtr tex = encodedRT != null ? encodedRT.GetNativeTexturePtr() : IntPtr.Zero;
        if (tex != IntPtr.Zero)
        {
            cb.IssuePluginEventAndData(NWR_GetRenderEventFunc(), 1, tex);
        }

        Graphics.ExecuteCommandBuffer(cb);
    }
}
