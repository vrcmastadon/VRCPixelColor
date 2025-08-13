using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>
/// Receives RAW color from PixelColorController, applies optional processing,
/// smooths it, and writes to targets (Lights, Renderers via MPB, ParticleSystems, PointLightVolumes).
/// AudioLink integration was intentionally removed to avoid CPU-side latency.
/// </summary>
public class PixelColorReceiver : UdonSharpBehaviour
{
    [Header("Source (Controller)")]
    [Tooltip("Reference to PixelColorController that provides the sampled RAW color.")]
    public PixelColorController controller;

    [Header("Targeting Mode")]
    [Tooltip("If enabled, manual lists are ignored and components are discovered automatically.")]
    public bool useAutoTargets = true;
    [Tooltip("In auto mode: also search for components in children.")]
    public bool autoIncludeChildren = true;

    [Header("Color Processing")]
    [Tooltip("Normalize brightness toward the brightest channel.")]
    public bool boostBrightness = true;
    [Tooltip("0 = off, 1 = fully normalize to the brightest channel.")]
    [Range(0f, 1f)] public float brightenAmount = 1f;

    [Tooltip("Boost or reduce saturation via HSV.")]
    public bool boostSaturation = true;
    [Tooltip("0 = grayscale, 1 = unchanged, >1 = boost (clamped to 1).")]
    [Range(0f, 5f)] public float saturationMultiplier = 1.5f;

    [Tooltip("If brightness is below threshold, force white.")]
    public bool blackToWhite = true;
    [Tooltip("Max channel <= threshold -> output becomes white.")]
    [Range(0f, 0.1f)] public float blackThreshold = 0.01f;

    [Tooltip("Final intensity multiplier applied after all processing.")]
    public float globalIntensity = 1f;

    [Header("Temporal Smoothing")]
    [Tooltip("0 = instant, 1 = heavy smoothing.")]
    [Range(0f, 1f)] public float smooth = 0.2f;

    [Header("Output: Unity Lights")]
    [Tooltip("Manual mode only: explicit list of Unity Lights to color.")]
    public Light[] targetLights;

    [Header("Output: Renderers")]
    [Tooltip("Manual mode only: explicit list of renderers to color via MaterialPropertyBlock.")]
    public Renderer[] targetRenderers;
    [Tooltip("Material color properties to set (e.g. _EmissionColor, _Color, _BaseColor).")]
    public string[] materialColorProperties = new string[] { "_EmissionColor" };

    [Header("Output: Particle Systems")]
    [Tooltip("Manual mode only: explicit list of particle systems to color.")]
    public ParticleSystem[] targetParticleSystems;
    [Tooltip("Apply color to ParticleSystem.Main.startColor.")]
    public bool particleApplyStartColor = false;
    [Tooltip("Apply color to ParticleSystem.ColorOverLifetime gradient.")]
    public bool particleApplyColorOverLifetime = true;
    [Tooltip("When using ColorOverLifetime, fade in/out alpha (0 -> peak -> 0).")]
    public bool particleFadeInOut = true;
    [Tooltip("Alpha for particle colors.")]
    [Range(0f, 1f)] public float particleAlpha = 1f;

    [Header("Output: Alternative Lights (PointLightVolume)")]
    [Tooltip("Manual mode only: UdonBehaviours of VRCLightVolumes (PointLightVolumeInstance). 'Color' program variable will be set.")]
    public UdonBehaviour[] targetPointLightVolumes;

    // Internal state
    private Color _smoothed = Color.white;

    // Auto-target caches
    private Light[] _autoLights;
    private Renderer[] _autoRenderers;
    private ParticleSystem[] _autoPS;
    private UdonBehaviour[] _autoPointLightVolumes;

    // Gradient cache for Particle ColorOverLifetime
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

        // Init gradient keys (constant times)
        _colGradient = new Gradient();
        _gradColorKeys2[0].time = 0f; _gradColorKeys2[1].time = 1f;
        _gradAlphaKeys2[0].time = 0f; _gradAlphaKeys2[1].time = 1f;
        _gradAlphaKeys3[0].time = 0f; _gradAlphaKeys3[1].time = 0.5f; _gradAlphaKeys3[2].time = 1f;

        _smoothed = controller.GetLastColorRaw();

        // Cache auto-targets if requested
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
    }

    void LateUpdate()
    {
        // 1) Fetch RAW color
        Color c = controller.GetLastColorRaw();

        // 2) Processing
        float maxCh = Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        if (blackToWhite && maxCh <= blackThreshold)
        {
            c = Color.white; maxCh = 1f;
        }
        else if (boostBrightness && maxCh > 0f)
        {
            // Normalize towards the max channel
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

        if (globalIntensity != 1f)
        {
            c *= Mathf.Max(0f, globalIntensity);
        }

        // 3) Temporal smoothing (exponential)
        float frames = Time.deltaTime * 60f;
        float perFrame = Mathf.Lerp(1f, 0.05f, Mathf.Clamp01(smooth)); // 0 -> instant, 1 -> heavy smoothing
        float t = 1f - Mathf.Pow(1f - perFrame, frames);
        _smoothed = Color.Lerp(_smoothed, c, t);

        // 4) Apply to targets
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
            // VRCLightVolumes typically expose 'Color' as a public program variable
            ub.SetProgramVariable("Color", col);
        }
    }

    // Public getter if other Udon scripts need the smoothed color
    public Color GetObjectColorSmoothed() { return _smoothed; }
}
