using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Zoom Blur")]
    public sealed class ZoomBlur : CreativeFxBase
    {
        public ClampedFloatParameter amount = new ClampedFloatParameter(0.42f, 0, 1);
        public ClampedFloatParameter innerRadius = new ClampedFloatParameter(0.08f, 0, 1);
        public ClampedFloatParameter outerRadius = new ClampedFloatParameter(0.86f, 0, 1.5f);
        public ClampedFloatParameter glow = new ClampedFloatParameter(0.18f, 0, 1);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ColorParameter tint = new ColorParameter(new Color(0.8f, 0.9f, 1), false, false, true);

        protected override int Mode => 25;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, amount.value);
            mat.SetFloat(ShaderIDs.Near, innerRadius.value);
            mat.SetFloat(ShaderIDs.Far, outerRadius.value);
            mat.SetFloat(ShaderIDs.Threshold, glow.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
            mat.SetColor(ShaderIDs.Color2, new Color(center.value.x, center.value.y, 0, 0));
        }
    }
}
