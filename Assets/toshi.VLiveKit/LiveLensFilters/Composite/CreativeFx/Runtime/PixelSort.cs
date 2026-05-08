using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Pixel Sort")]
    public sealed class PixelSort : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.65f, 0, 2);
        public ClampedFloatParameter length = new ClampedFloatParameter(0.45f, 0, 1);

        protected override int Mode => 8;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, length.value);
        }
    }
}
