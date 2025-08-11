using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;

public class PixelColorController : UdonSharpBehaviour
{
    [Header("Source")]
    [Tooltip("RenderTexture из VideoTXL (или другая Texture)")]
    public RenderTexture sourceRT;
    [Tooltip("UV координаты (0..1) точки выборки")]
    public Vector2 uv = new Vector2(0.5f, 0.5f);

    [Header("Blit Material (Unlit/PickUV_Udon)")]
    [Tooltip("Материал на шейдере Unlit/PickUV_Udon")]
    public Material pickUVMaterial;

    [Header("Sampling")]
    [Tooltip("Сэмплировать раз в N кадров (1 = каждый кадр)")]
    public int sampleEveryNFrames = 1;

    [Header("Debug")]
    public bool debugLog = true;
    [Tooltip("Сюда можно повесить Quad/Plane — покажем превью")]
    public Renderer debugPreviewReceiver;
    public string debugPreviewProperty = "_MainTex";

    // --- внутреннее ---
    private RenderTexture _rt1x1;              // сырая 1×1 RT из источника
    private bool _readbackPending;
    private int _frame;
    private readonly Color32[] _pxBuf = new Color32[1]; // буфер 1 пикселя

    // Публичный доступ для ресиверов
    private Color _lastSampled = Color.white;  // СЫРОЙ цвет из текстуры без обработки
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

        // 1×1 RT (ARGB32 sRGB) — сырая выборка
        _rt1x1 = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        _rt1x1.wrapMode = TextureWrapMode.Clamp;
        _rt1x1.filterMode = FilterMode.Point;
        _rt1x1.Create();

        // Debug-превью: показываем сырую 1×1 RT (без обработки)
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

        if ((_frame % sampleEveryNFrames) != 0)
        {
            return;
        }

        if (_readbackPending || sourceRT == null) { return; }

        // передаём UV и ИСТОЧНИК в материал (важно!)
        float u = Mathf.Clamp01(uv.x);
        float v = Mathf.Clamp01(uv.y);
        pickUVMaterial.SetVector("_UV", new Vector4(u, v, 0, 0));
        pickUVMaterial.SetTexture("_MainTex", sourceRT);

        // Udon-совместимый Blit (с явным pass=0)
        VRCGraphics.Blit(sourceRT, _rt1x1, pickUVMaterial, 0);

        // Асинхронное чтение 1 пикселя (из сырой 1x1 RT)
        _readbackPending = true;
        if (debugLog && (Time.frameCount % 30 == 0)) Debug.Log("[PixelColorController] Request readback");
        VRCAsyncGPUReadback.Request(_rt1x1, 0, (IUdonEventReceiver)this);
    }

    // колбэки readback (под разные версии SDK)
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

        // сырой цвет 1×1
        Color32 c32 = _pxBuf[0];
        _lastSampled = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f);

        if (debugLog && (Time.frameCount % 30 == 0))
            Debug.Log("[PixelColorController] RAW=" + _lastSampled);
    }
}
