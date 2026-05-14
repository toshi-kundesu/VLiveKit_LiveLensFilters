using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Water Droplets")]
    public sealed class WaterDroplets : CreativeFxBase
    {
        public ClampedFloatParameter density = new ClampedFloatParameter(0.56f, 0, 1);
        public ClampedFloatParameter size = new ClampedFloatParameter(0.48f, 0, 1);
        public ClampedFloatParameter refraction = new ClampedFloatParameter(0.62f, 0, 1);
        public ClampedFloatParameter highlight = new ClampedFloatParameter(0.72f, 0, 1);
        public ClampedFloatParameter fallSpeed = new ClampedFloatParameter(0.25f, 0, 1);
        public ColorParameter tint = new ColorParameter(new Color(0.86f, 0.95f, 1, 0.24f), false, true, true);

        protected override int Mode => 21;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, density.value);
            mat.SetFloat(ShaderIDs.Radius, size.value);
            mat.SetFloat(ShaderIDs.Threshold, refraction.value);
            mat.SetFloat(ShaderIDs.Near, highlight.value);
            mat.SetFloat(ShaderIDs.Far, fallSpeed.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
