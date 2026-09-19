using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Lens Distortion")]
    public sealed class LensDistortionFx : CreativeFxBase
    {
        public ClampedFloatParameter amount = new ClampedFloatParameter(0.49f, 0, 1);
        public ClampedFloatParameter chromatic = new ClampedFloatParameter(0.51f, 0, 1);

        protected override int Mode => 12;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, amount.value);
            mat.SetFloat(ShaderIDs.Radius, chromatic.value);
        }
    }
}
