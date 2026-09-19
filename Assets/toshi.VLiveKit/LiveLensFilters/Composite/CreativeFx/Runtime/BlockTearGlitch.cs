using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Block Tear Glitch")]
    public sealed class BlockTearGlitch : CreativeFxBase
    {
        public ClampedFloatParameter probability = new ClampedFloatParameter(0.29f, 0, 1);
        public ClampedFloatParameter displacement = new ClampedFloatParameter(0.63f, 0, 1);
        public ClampedFloatParameter blockSize = new ClampedFloatParameter(0.68f, 0, 1);
        public ClampedIntParameter quantizeSteps = new ClampedIntParameter(15, 2, 32);

        protected override int Mode => 19;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, probability.value);
            mat.SetFloat(ShaderIDs.Amount, displacement.value);
            mat.SetFloat(ShaderIDs.Radius, blockSize.value);
            mat.SetInt(ShaderIDs.Steps, quantizeSteps.value);
        }
    }
}
