using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Scan Roll Glitch")]
    public sealed class ScanRollGlitch : CreativeFxBase
    {
        public ClampedFloatParameter speed = new ClampedFloatParameter(0.4f, 0, 1);
        public ClampedFloatParameter frequency = new ClampedFloatParameter(0.35f, 0, 1);

        protected override int Mode => 20;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, speed.value);
            mat.SetFloat(ShaderIDs.Radius, frequency.value);
        }
    }
}
