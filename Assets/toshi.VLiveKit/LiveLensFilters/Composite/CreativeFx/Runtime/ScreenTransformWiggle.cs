using UnityEngine;
using UnityEngine.Rendering;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("toshi/VLiveKit/LiveLensFilters/Screen Transform Wiggle")]
    public sealed class ScreenTransformWiggle : MonoBehaviour
    {
        [SerializeField] Volume volume;
        [SerializeField] bool createEffectIfMissing = true;
        [Min(0f)] public float frequency = 2f;
        [SerializeField] int seed = 1138;
        [SerializeField, Range(0f, 1f)] float intensity = 1f;
        [SerializeField] Vector2 baseOffset = Vector2.zero;
        [SerializeField] Vector2 offsetAmplitude = new Vector2(0.012f, 0.008f);
        [SerializeField, Min(0.01f)] float baseZoom = 1.04f;
        [SerializeField, Min(0f)] float zoomAmplitude = 0.025f;
        [SerializeField] float baseRotation;
        [SerializeField, Min(0f)] float rotationAmplitude = 0.35f;
        [SerializeField] Vector2 pivot = new Vector2(0.5f, 0.5f);
        [SerializeField] bool animateInEditMode;
        [SerializeField] bool useUnscaledTime = true;

        VolumeProfile activeProfile;
        ScreenTransform effect;

        void Reset()
        {
            volume = GetComponent<Volume>();
        }

        void OnEnable()
        {
            EnsureEffect();
        }

        void OnValidate()
        {
            frequency = Mathf.Max(0f, frequency);
            intensity = Mathf.Clamp01(intensity);
            baseZoom = Mathf.Max(0.01f, baseZoom);
            zoomAmplitude = Mathf.Max(0f, zoomAmplitude);
            rotationAmplitude = Mathf.Max(0f, rotationAmplitude);
            activeProfile = null;
            effect = null;
        }

        void Update()
        {
            if (!Application.isPlaying && !animateInEditMode)
                return;

            ApplyWiggle();
        }

        void ApplyWiggle()
        {
            if (!EnsureEffect())
                return;

            var t = useUnscaledTime || !Application.isPlaying ? Time.realtimeSinceStartup : Time.time;
            var noiseTime = t * frequency;
            var offset = baseOffset + new Vector2(
                SignedNoise(noiseTime, 0) * offsetAmplitude.x,
                SignedNoise(noiseTime, 1) * offsetAmplitude.y);
            var zoom = Mathf.Max(0.01f, baseZoom + SignedNoise(noiseTime, 2) * zoomAmplitude);
            var rotation = baseRotation + SignedNoise(noiseTime, 3) * rotationAmplitude;

            effect.active = true;
            Override(effect.intensity, intensity);
            Override(effect.offset, offset);
            Override(effect.zoom, zoom);
            Override(effect.rotation, rotation);
            Override(effect.pivot, pivot);
        }

        bool EnsureEffect()
        {
            if (volume == null)
                volume = GetComponent<Volume>();

            if (volume == null)
                return false;

            var profile = volume.profile;
            if (profile == null)
            {
                if (!createEffectIfMissing)
                    return false;

                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Runtime Screen Transform Wiggle Profile";
                profile.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                volume.sharedProfile = profile;
            }

            if (profile != activeProfile)
            {
                activeProfile = profile;
                effect = null;
            }

            if (effect != null)
                return true;

            if (!profile.TryGet(out effect) && createEffectIfMissing)
                effect = profile.Add<ScreenTransform>(true);

            return effect != null;
        }

        float SignedNoise(float time, int channel)
        {
            var seedOffset = seed * 0.0137f + channel * 23.173f;
            return Mathf.PerlinNoise(seedOffset, time + channel * 11.91f) * 2f - 1f;
        }

        static void Override<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }
    }
}
