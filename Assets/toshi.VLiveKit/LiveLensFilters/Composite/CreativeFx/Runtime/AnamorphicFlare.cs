using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Anamorphic Flare")]
    public sealed class AnamorphicFlare : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.75f, 0, 5);
        public ClampedFloatParameter length = new ClampedFloatParameter(0.65f, 0, 1);
        public ColorParameter tint = new ColorParameter(new Color(0.38f, 0.58f, 1), false, false, true);

        protected override int Mode => 13;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, length.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
