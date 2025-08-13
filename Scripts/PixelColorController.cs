using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;

public class PixelColorController : UdonSharpBehaviour
{
    [Header("Source")]
    [Tooltip("RenderTexture from VideoTXL (or any Texture)")]
    public RenderTexture sourceRT;
    [Tooltip("UV coordinates (0..1) for the sampling point")]
    public Vector2 uv = new Vector2(0.5f, 0.5f);

    [Header("Blit Material (Unlit/PickUV_Udon)")]
    [Tooltip("Material that uses Unlit/PickUV_Udon shader")]
    public Material pickUVMaterial;

    [Header("Sampling")]
    [Tooltip("Sample every N frames (1 = every frame)")]
    public int sampleEveryNFrames = 1;

    [Header("Debug")]
    public bool debugLog = true;
    [Tooltip("Optional preview receiver (Quad/Plane) to show the raw 1x1 RT")]
    public Renderer debugPreviewReceiver;
    public string debugPreviewProperty = "_MainTex";

    // --- internal ---
    private RenderTexture _rt1x1;              // raw 1×1 RT from source
    private bool _readbackPending;
    private int _frame;
    private readonly Color32[] _pxBuf = new Color32[1]; // 1-pixel buffer

    // Public raw color for receivers
    private Color _lastSampled = Color.white;  // RAW color from texture (no processing)
    public Color GetLastColorRaw() { return _lastSampled; }

    void Start()
    {
        if (sourceRT == null || pickUVMaterial == null)
        {
            Debug.LogError("[PixelColorController] Assign sourceRT and pickUVMaterial (Unlit/PickUV_Udon).");
            enabled = false;
            return;
        }

        if (sampleEveryNFrames < 1) sampleEveryNFrames = 1;

        // Create 1×1 RT (ARGB32 sRGB) for raw sampling
        _rt1x1 = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        _rt1x1.wrapMode = TextureWrapMode.Clamp;
        _rt1x1.filterMode = FilterMode.Point;
        _rt1x1.Create();

        // Optional debug preview
        if (debugPreviewReceiver != null)
        {
            var mpb = new MaterialPropertyBlock();
            debugPreviewReceiver.GetPropertyBlock(mpb);
            mpb.SetTexture(debugPreviewProperty, _rt1x1);
            debugPreviewReceiver.SetPropertyBlock(mpb);
        }

        if (debugLog) Debug.Log("[PixelColorController] Start OK");
    }

    void OnDestroy()
    {
        if (_rt1x1 != null) _rt1x1.Release();
        _rt1x1 = null;
    }

    void LateUpdate()
    {
        _frame++;

        if ((_frame % sampleEveryNFrames) != 0) return;
        if (_readbackPending || sourceRT == null) return;

        // Feed UV and SOURCE into the material
        float u = Mathf.Clamp01(uv.x);
        float v = Mathf.Clamp01(uv.y);
        pickUVMaterial.SetVector("_UV", new Vector4(u, v, 0, 0));
        pickUVMaterial.SetTexture("_MainTex", sourceRT);

        // Udon-compatible Blit (explicit pass = 0) into 1×1 RT
        VRCGraphics.Blit(sourceRT, _rt1x1, pickUVMaterial, 0);

        // Async GPU readback of the 1×1 pixel
        _readbackPending = true;
        if (debugLog && (Time.frameCount % 30 == 0)) Debug.Log("[PixelColorController] Request readback");
        VRCAsyncGPUReadback.Request(_rt1x1, 0, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);
    }

    // Readback callbacks (SDK variants)
    public void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request) { HandleReadback(request); }
    public void OnAsyncGPUReadbackComplete(VRCAsyncGPUReadbackRequest request) { HandleReadback(request); }

    private void HandleReadback(VRCAsyncGPUReadbackRequest request)
    {
        _readbackPending = false;

        if (request.hasError)
        {
            if (debugLog) Debug.LogWarning("[PixelColorController] GPU readback error");
            return;
        }

        if (!request.TryGetData(_pxBuf))
        {
            if (debugLog) Debug.LogWarning("[PixelColorController] TryGetData failed");
            return;
        }

        // Store RAW color (no processing here)
        Color32 c32 = _pxBuf[0];
        _lastSampled = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f);

        if (debugLog && (Time.frameCount % 30 == 0))
            Debug.Log("[PixelColorController] RAW=" + _lastSampled);
    }
}
