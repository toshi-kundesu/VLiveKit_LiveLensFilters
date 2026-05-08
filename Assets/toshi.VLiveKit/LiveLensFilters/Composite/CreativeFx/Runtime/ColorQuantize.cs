using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Color Quantize")]
    public sealed class ColorQuantize : CreativeFxBase
    {
        public ClampedIntParameter steps = new ClampedIntParameter(6, 2, 32);
        public ClampedFloatParameter dither = new ClampedFloatParameter(0.25f, 0, 1);

        protected override int Mode => 9;

        protected override void SetParameters(Material mat)
        {
            mat.SetInt(ShaderIDs.Steps, steps.value);
            mat.SetFloat(ShaderIDs.Amount, dither.value);
        }
    }
}
