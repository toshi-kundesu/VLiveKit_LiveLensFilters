using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Star Filter")]
    public sealed class StarFilter : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.8f, 0, 4);
        public ClampedFloatParameter length = new ClampedFloatParameter(0.55f, 0, 1);

        protected override int Mode => 5;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Radius, length.value);
        }
    }
}
