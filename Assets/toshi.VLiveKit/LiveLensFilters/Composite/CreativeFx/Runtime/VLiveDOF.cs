using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/VLiveDOF")]
    public sealed class VLiveDOF : CreativeFxBase
    {
        public MinFloatParameter focusDistance = new MinFloatParameter(5.0f, 0.01f);
        public MinFloatParameter focusRange = new MinFloatParameter(1.5f, 0.01f);
        public ClampedFloatParameter blurRadius = new ClampedFloatParameter(8.0f, 0.0f, 24.0f);
        public ClampedFloatParameter nearBlur = new ClampedFloatParameter(0.55f, 0.0f, 1.0f);
        public ClampedFloatParameter farBlur = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
        public ClampedFloatParameter bokehThreshold = new ClampedFloatParameter(0.9f, 0.0f, 8.0f);
        public ClampedFloatParameter bokehIntensity = new ClampedFloatParameter(0.8f, 0.0f, 4.0f);
        public ClampedIntParameter samples = new ClampedIntParameter(18, 4, 24);
        public ColorParameter bokehTint = new ColorParameter(Color.white, false, false, true);

        protected override int Mode => 26;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Near, focusDistance.value);
            mat.SetFloat(ShaderIDs.Far, Mathf.Max(0.01f, focusRange.value));
            mat.SetFloat(ShaderIDs.Radius, blurRadius.value);
            mat.SetFloat(ShaderIDs.Threshold, bokehThreshold.value);
            mat.SetFloat(ShaderIDs.Amount, bokehIntensity.value);
            mat.SetInt(ShaderIDs.Steps, samples.value);
            mat.SetColor(ShaderIDs.Color, bokehTint.value);
            mat.SetColor(ShaderIDs.Color2, new Color(nearBlur.value, farBlur.value, 0.0f, 0.0f));
        }
    }
}
