using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Halation")]
    public sealed class Halation : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.45f, 0, 4);
        public ClampedFloatParameter radius = new ClampedFloatParameter(6, 0, 16);
        public ColorParameter tint = new ColorParameter(new Color(1, 0.38f, 0.18f), false, false, true);

        protected override int Mode => 0;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, radius.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
