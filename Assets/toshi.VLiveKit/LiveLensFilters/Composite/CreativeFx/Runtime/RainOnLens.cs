using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Rain On Lens")]
    public sealed class RainOnLens : CreativeFxBase
    {
        public ClampedFloatParameter rainAmount = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter dropletSize = new ClampedFloatParameter(0.48f, 0f, 1f);
        public ClampedFloatParameter refraction = new ClampedFloatParameter(0.68f, 0f, 1f);
        public ClampedFloatParameter highlight = new ClampedFloatParameter(0.72f, 0f, 1f);
        public ClampedFloatParameter fallSpeed = new ClampedFloatParameter(0.35f, 0f, 1f);
        public ColorParameter tint = new ColorParameter(new Color(0.86f, 0.95f, 1f, 0.24f), false, true, true);

        protected override int Mode => 29;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, rainAmount.value);
            mat.SetFloat(ShaderIDs.Radius, dropletSize.value);
            mat.SetFloat(ShaderIDs.Threshold, refraction.value);
            mat.SetFloat(ShaderIDs.Near, highlight.value);
            mat.SetFloat(ShaderIDs.Far, fallSpeed.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
