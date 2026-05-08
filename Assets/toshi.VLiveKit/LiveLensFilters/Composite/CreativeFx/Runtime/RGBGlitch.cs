using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/RGB Glitch")]
    public sealed class RGBGlitch : CreativeFxBase
    {
        public ClampedFloatParameter probability = new ClampedFloatParameter(0.18f, 0, 1);
        public ClampedFloatParameter displacement = new ClampedFloatParameter(0.45f, 0, 1);
        public ClampedFloatParameter bandDensity = new ClampedFloatParameter(0.45f, 0, 1);

        protected override int Mode => 18;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, probability.value);
            mat.SetFloat(ShaderIDs.Amount, displacement.value);
            mat.SetFloat(ShaderIDs.Radius, bandDensity.value);
        }
    }
}
