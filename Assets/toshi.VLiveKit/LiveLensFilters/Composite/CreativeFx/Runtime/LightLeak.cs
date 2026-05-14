using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Light Leak")]
    public sealed class LightLeak : CreativeFxBase
    {
        public ColorParameter warm = new ColorParameter(new Color(1, 0.42f, 0.12f), false, false, true);
        public ColorParameter cool = new ColorParameter(new Color(0.15f, 0.75f, 1), false, false, true);
        public ClampedFloatParameter drift = new ClampedFloatParameter(0.25f, 0, 1);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0.62f, 0, 1);
        public ClampedFloatParameter burn = new ClampedFloatParameter(0.58f, 0, 1);

        protected override int Mode => 4;

        protected override void SetParameters(Material mat)
        {
            mat.SetColor(ShaderIDs.Color, warm.value);
            mat.SetColor(ShaderIDs.Color2, cool.value);
            mat.SetFloat(ShaderIDs.Amount, drift.value);
            mat.SetFloat(ShaderIDs.Radius, softness.value);
            mat.SetFloat(ShaderIDs.Threshold, burn.value);
        }
    }
}
