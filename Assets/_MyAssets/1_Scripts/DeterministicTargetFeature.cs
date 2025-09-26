using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System;
using System.Runtime.InteropServices;
using System.Text;

public class DeterministicTargetFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Range(8, 2048)] public int width = 1920;
        [Range(8, 2048)] public int height = 1080;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;  // after post
        public Material overlayMaterial; // assign the HUD material
        public string textureGlobalName = "_DeterministicColor";
        public RenderTexture DeterministicRT;
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        readonly int _width, _height;
        readonly string _globalName;
        readonly Material _overlayMat;
        RenderTexture _fixedRT;
        readonly bool _echoToScreen = false;

        // IDs
        static readonly int _FrameID = Shader.PropertyToID("_FrameIndex");
        static readonly int _TimeID = Shader.PropertyToID("_TimeNow");
        static readonly int _TargetSize = Shader.PropertyToID("_TargetSize"); // float2(width,height)
        static readonly int _HudScale = Shader.PropertyToID("_HudScale");   // float


        public Pass(DeterministicTargetFeature.Settings s)
        {
            _width = Mathf.Max(8, s.width);
            _height = Mathf.Max(8, s.height);
            _overlayMat = s.overlayMaterial;
            _globalName = s.textureGlobalName;
            _fixedRT = s.DeterministicRT;

            renderPassEvent = s.passEvent;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        void EnsureRT()
        {
            if (_fixedRT != null && (_fixedRT.width == _width && _fixedRT.height == _height)) return;

           // if (_fixedRT != null) { _fixedRT.Release(); Object.DestroyImmediate(_fixedRT); }

            _fixedRT = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Deterministic_1920x1080_ARGB32"
            };
            _fixedRT.Create();
        }

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            if (data.cameraData.isPreviewCamera) return;

            EnsureRT();

            var cmd = CommandBufferPool.Get("DeterministicTarget");
            var src = (RenderTargetIdentifier)BuiltinRenderTextureType.CurrentActive;

            // 1) Copy current active camera color -> fixed RT
            cmd.Blit(src, _fixedRT);

            // 2) HUD overlay (in-place)
            if (_overlayMat)
            {
                var pidFlip = Shader.PropertyToID("_HudFlip");
                _overlayMat.SetVector(pidFlip, new Vector4(1f, 1f, 0f, 0f));
                _overlayMat.SetInt(Shader.PropertyToID("_FrameIndex"), Time.frameCount);
                _overlayMat.SetFloat(Shader.PropertyToID("_TimeNow"), Time.realtimeSinceStartup);
                _overlayMat.SetVector(Shader.PropertyToID("_TargetSize"), new Vector4(_fixedRT.width, _fixedRT.height, 0, 0));
                _overlayMat.SetFloat(Shader.PropertyToID("_HudScale"), 1.8f); // 1.5–2.0 is a good starting point


                cmd.Blit(_fixedRT, _fixedRT, _overlayMat, 0);
            }

            // 3) Expose globally
            cmd.SetGlobalTexture(_globalName, _fixedRT);

            
            if (_echoToScreen) cmd.Blit(_fixedRT, src);

            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        public void Dispose()
        {
            if (_fixedRT != null) { _fixedRT.Release(); DestroyImmediate(_fixedRT); _fixedRT = null; }
        }
    }

    Pass m_Pass;

    public override void Create() => m_Pass = new Pass(settings);

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        // Ensuring determinism: camera shouldn't inject MSAA into our copy
        data.cameraData.camera.allowMSAA = false;
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        m_Pass?.Dispose();
    }
}
