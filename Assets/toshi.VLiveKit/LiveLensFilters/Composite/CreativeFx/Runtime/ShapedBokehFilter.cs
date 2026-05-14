using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    public enum ShapedBokehPattern
    {
        Forest,
        Star,
        Heart,
        Circle
    }

    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Shaped Bokeh Filter")]
    public sealed class ShapedBokehFilter : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.85f, 0.0f, 8.0f);
        public ClampedFloatParameter size = new ClampedFloatParameter(0.55f, 0.0f, 1.0f);
        public ClampedFloatParameter bokehIntensity = new ClampedFloatParameter(1.15f, 0.0f, 4.0f);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0.18f, 0.0f, 1.0f);
        public ClampedFloatParameter rotation = new ClampedFloatParameter(0.0f, -1.0f, 1.0f);
        public ClampedIntParameter samples = new ClampedIntParameter(7, 3, 9);
        public EnumParameter<ShapedBokehPattern> pattern =
            new EnumParameter<ShapedBokehPattern>(ShapedBokehPattern.Forest);
        public Texture2DParameter patternTexture = new Texture2DParameter(null);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        protected override int Mode => 27;

        protected override void SetParameters(Material mat)
        {
            var sampleCount = Mathf.Clamp(samples.value, 3, 9);
            if ((sampleCount & 1) == 0)
                sampleCount += sampleCount < 9 ? 1 : -1;

            var texture = patternTexture.value;

            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, size.value);
            mat.SetFloat(ShaderIDs.Amount, bokehIntensity.value);
            mat.SetFloat(ShaderIDs.Softness, softness.value);
            mat.SetFloat(ShaderIDs.Rotation, rotation.value * Mathf.PI);
            mat.SetInt(ShaderIDs.Steps, sampleCount);
            mat.SetInt(ShaderIDs.Pattern, (int)pattern.value);
            mat.SetFloat(ShaderIDs.UsePatternTexture, texture != null ? 1.0f : 0.0f);
            mat.SetTexture(ShaderIDs.PatternTexture, texture != null ? texture : Texture2D.whiteTexture);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
