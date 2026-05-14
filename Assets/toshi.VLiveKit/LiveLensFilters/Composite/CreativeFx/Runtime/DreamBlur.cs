using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Dream Blur")]
    public sealed class DreamBlur : CreativeFxBase
    {
        public ClampedFloatParameter radius = new ClampedFloatParameter(2, 0, 10);
        public ClampedFloatParameter lift = new ClampedFloatParameter(0.15f, 0, 1);

        protected override int Mode => 6;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Radius, radius.value);
            mat.SetFloat(ShaderIDs.Amount, lift.value);
        }
    }
}
