using UdonSharp;
using UnityEngine;
using VRC.Udon;
#if AUDIOLINK
using AudioLink;
#endif

// Top-level enum (UdonSharp не поддерживает вложенные)
public enum PixelColorAudioBand { Bass = 0, LowMid = 1, HighMid = 2, Treble = 3 }

public class PixelColorReceiver : UdonSharpBehaviour
{
    [Header("Source (Controller)")]
    public PixelColorController controller;

    [Header("Targeting Mode")]
    public bool useAutoTargets = true;
    public bool autoIncludeChildren = true;

    [Header("Color Processing")]
    public bool boostBrightness = true;
    [Range(0f, 1f)] public float brightenAmount = 1f;
    public bool boostSaturation = true;
    [Range(0f, 5f)] public float saturationMultiplier = 1.5f;
    public bool blackToWhite = true;
    [Range(0f, 0.1f)] public float blackThreshold = 0.01f;
    public float globalIntensity = 1f;

#if AUDIOLINK
    [Header("AudioLink")]
    public AudioLink.AudioLink audioLink;
    public bool useAudioLink = false;
    public PixelColorAudioBand audioBand = PixelColorAudioBand.Bass;
    [Range(0, 127)] public int audioDelay = 0;
    [Tooltip("Brightness multiplier when AudioLink signal is at peak (default 2)")]
    public float audioLinkPeakMultiplier = 2f;
#endif

    [Header("Temporal Smoothing")]
    [Range(0f, 1f)] public float smooth = 0.2f;

    [Header("Output: Unity Lights")]
    public Light[] targetLights;

    [Header("Output: Renderers")]
    public Renderer[] targetRenderers;
    public string[] materialColorProperties = new string[] { "_EmissionColor" };

    [Header("Output: Particle Systems")]
    public ParticleSystem[] targetParticleSystems;
    public bool particleApplyStartColor = false;
    public bool particleApplyColorOverLifetime = true;
    public bool particleFadeInOut = true;
    [Range(0f, 1f)] public float particleAlpha = 1f;

    [Header("Output: Alternative Lights (PointLightVolume)")]
    public UdonBehaviour[] targetPointLightVolumes;

    // Internal
    private Color _smoothed = Color.white;

    private Light[] _autoLights;
    private Renderer[] _autoRenderers;
    private ParticleSystem[] _autoPS;
    private UdonBehaviour[] _autoPointLightVolumes;

    private Gradient _colGradient;
    private GradientColorKey[] _gradColorKeys2 = new GradientColorKey[2];
    private GradientAlphaKey[] _gradAlphaKeys2 = new GradientAlphaKey[2];
    private GradientAlphaKey[] _gradAlphaKeys3 = new GradientAlphaKey[3];

    void Start()
    {
        if (controller == null)
        {
            Debug.LogError("[PixelColorReceiver] Assign PixelColorController.");
            enabled = false;
            return;
        }

        _colGradient = new Gradient();
        _gradColorKeys2[0].time = 0f; _gradColorKeys2[1].time = 1f;
        _gradAlphaKeys2[0].time = 0f; _gradAlphaKeys2[1].time = 1f;
        _gradAlphaKeys3[0].time = 0f; _gradAlphaKeys3[1].time = 0.5f; _gradAlphaKeys3[2].time = 1f;

        _smoothed = controller.GetLastColorRaw();

        if (useAutoTargets)
        {
            if (autoIncludeChildren)
            {
                _autoLights = GetComponentsInChildren<Light>(true);
                _autoRenderers = GetComponentsInChildren<Renderer>(true);
                _autoPS = GetComponentsInChildren<ParticleSystem>(true);
                _autoPointLightVolumes = GetComponentsInChildren<UdonBehaviour>(true);
            }
            else
            {
                _autoLights = GetComponents<Light>();
                _autoRenderers = GetComponents<Renderer>();
                _autoPS = GetComponents<ParticleSystem>();
                _autoPointLightVolumes = GetComponents<UdonBehaviour>();
            }
        }

#if AUDIOLINK
        if (audioLink != null) audioLink.EnableReadback();
#endif
    }

    void LateUpdate()
    {
        Color c = controller.GetLastColorRaw();

        float maxCh = Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        if (blackToWhite && maxCh <= blackThreshold)
        {
            c = Color.white; maxCh = 1f;
        }
        else if (boostBrightness && maxCh > 0f)
        {
            float kFull = 1f / maxCh;
            float k = Mathf.Lerp(1f, kFull, Mathf.Clamp01(brightenAmount));
            c *= k;
        }

        if (boostSaturation)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            s *= saturationMultiplier;
            s = Mathf.Clamp01(s);
            c = Color.HSVToRGB(h, s, v);
        }

#if AUDIOLINK
        if (useAudioLink && audioLink != null)
        {
            int band = (int)audioBand;
            Color sample = audioLink.GetDataAtPixel(audioDelay, band);
            float amp = Mathf.Max(sample.r, Mathf.Max(sample.g, sample.b));
            amp = Mathf.Clamp01(amp);

            // Применяем множитель в момент пика
            c *= amp * audioLinkPeakMultiplier;
        }
#endif

        if (globalIntensity != 1f)
        {
            c *= Mathf.Max(0f, globalIntensity);
        }

        float frames = Time.deltaTime * 60f;
        float perFrame = Mathf.Lerp(1f, 0.05f, Mathf.Clamp01(smooth));
        float t = 1f - Mathf.Pow(1f - perFrame, frames);
        _smoothed = Color.Lerp(_smoothed, c, t);

        if (useAutoTargets)
        {
            ApplyLights(_autoLights, _smoothed);
            ApplyRenderers(_autoRenderers, _smoothed);
            ApplyParticles(_autoPS, _smoothed);
            ApplyPointLightVolumes(_autoPointLightVolumes, _smoothed);
        }
        else
        {
            ApplyLights(targetLights, _smoothed);
            ApplyRenderers(targetRenderers, _smoothed);
            ApplyParticles(targetParticleSystems, _smoothed);
            ApplyPointLightVolumes(targetPointLightVolumes, _smoothed);
        }
    }

    private void ApplyLights(Light[] lights, Color col)
    {
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++)
        {
            Light L = lights[i];
            if (L == null) continue;
            L.color = col;
        }
    }

    private void ApplyRenderers(Renderer[] rends, Color col)
    {
        if (rends == null || materialColorProperties == null) return;

        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null) continue;

            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            for (int p = 0; p < materialColorProperties.Length; p++)
            {
                string prop = materialColorProperties[p];
                if (!string.IsNullOrEmpty(prop)) mpb.SetColor(prop, col);
            }

            r.SetPropertyBlock(mpb);
        }
    }

    private void ApplyParticles(ParticleSystem[] systems, Color col)
    {
        if (systems == null || systems.Length == 0) return;

        float a = Mathf.Clamp01(particleAlpha);
        Color pc = new Color(col.r, col.g, col.b, a);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            if (particleApplyStartColor)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(pc);
            }

            if (particleApplyColorOverLifetime)
            {
                var colLifetime = ps.colorOverLifetime;
                colLifetime.enabled = true;

                _gradColorKeys2[0].color = pc;
                _gradColorKeys2[1].color = pc;

                if (particleFadeInOut)
                {
                    _gradAlphaKeys3[0].alpha = 0f;
                    _gradAlphaKeys3[1].alpha = pc.a;
                    _gradAlphaKeys3[2].alpha = 0f;
                    _colGradient.SetKeys(_gradColorKeys2, _gradAlphaKeys3);
                }
                else
                {
                    _gradAlphaKeys2[0].alpha = pc.a;
                    _gradAlphaKeys2[1].alpha = pc.a;
                    _colGradient.SetKeys(_gradColorKeys2, _gradAlphaKeys2);
                }

                colLifetime.color = new ParticleSystem.MinMaxGradient(_colGradient);
            }
        }
    }

    private void ApplyPointLightVolumes(UdonBehaviour[] volumes, Color col)
    {
        if (volumes == null) return;
        for (int i = 0; i < volumes.Length; i++)
        {
            var ub = volumes[i];
            if (ub == null) continue;
            ub.SetProgramVariable("Color", col);
        }
    }

    public Color GetObjectColorSmoothed() { return _smoothed; }
}
