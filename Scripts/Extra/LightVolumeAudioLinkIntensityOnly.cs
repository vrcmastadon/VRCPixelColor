using UnityEngine;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace VRCLightVolumes
{
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LightVolumeAudioLinkIntensityOnly : UdonSharpBehaviour
#else
    public class LightVolumeAudioLinkIntensityOnly : MonoBehaviour
#endif
    {
#if AUDIOLINK
        [Tooltip("Reference to your Audio Link manager that should control Light Volumes")]
        public AudioLink.AudioLink AudioLink;
        [Tooltip("Defines which audio band will be used to control Light Volumes")]
        public AudioLinkBand AudioBand = AudioLinkBand.Bass;
        [Range(0, 127)]
        public int Delay = 0;
        public bool SmoothingEnabled = true;
        [Range(0, 1)]
        public float Smoothing = 0.25f;

        [Tooltip("Minimum intensity multiplier")]
        public float MinIntensity = 0.2f;
        [Tooltip("Maximum intensity multiplier")]
        public float MaxIntensity = 1.0f;

        [Tooltip("List of the Light Volumes that should be affected")]
        public UdonBehaviour[] TargetLightVolumes;
        [Tooltip("List of the Point Light Volumes that should be affected")]
        public UdonBehaviour[] TargetPointLightVolumes;

        private float _prevLevel;

        private void Start()
        {
            if (AudioLink != null)
            {
                AudioLink.EnableReadback();
            }
        }

        private void Update()
        {
            if (AudioLink == null) return;

            int band = (int)AudioBand;
            Color sample = AudioLink.GetDataAtPixel(Delay, band);
            float level = Mathf.Max(sample.r, sample.g, sample.b);

            if (SmoothingEnabled)
            {
                float t = Time.deltaTime / Mathf.Lerp(0.25f, 1f, Smoothing);
                _prevLevel = Mathf.Lerp(_prevLevel, level, t);
            }
            else
            {
                _prevLevel = level;
            }

            float mappedIntensity = Mathf.Lerp(MinIntensity, MaxIntensity, _prevLevel);

            for (int i = 0; i < TargetLightVolumes.Length; i++)
            {
                if (TargetLightVolumes[i] != null)
                {
                    TargetLightVolumes[i].SetProgramVariable("Intensity", mappedIntensity);
                }
            }

            for (int i = 0; i < TargetPointLightVolumes.Length; i++)
            {
                if (TargetPointLightVolumes[i] != null)
                {
                    TargetPointLightVolumes[i].SetProgramVariable("Intensity", mappedIntensity);
                    TargetPointLightVolumes[i].SetProgramVariable("IsRangeDirty", true);
                }
            }
        }

        private void OnValidate()
        {
            if (AudioLink != null)
            {
                AudioLink.EnableReadback();
            }
        }
#endif
    }
}
