using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Light Wrap")]
    public sealed class LightWrap : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.55f, 0, 4);
        public ClampedFloatParameter radius = new ClampedFloatParameter(4, 0, 16);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        protected override int Mode => 16;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, radius.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
