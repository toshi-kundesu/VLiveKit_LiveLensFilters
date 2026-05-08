using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Analog Damage")]
    public sealed class AnalogDamage : CreativeFxBase
    {
        public ClampedFloatParameter noise = new ClampedFloatParameter(0.35f, 0, 1);
        public ClampedFloatParameter scanlines = new ClampedFloatParameter(0.4f, 0, 1);

        protected override int Mode => 2;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, noise.value);
            mat.SetFloat(ShaderIDs.Radius, scanlines.value);
        }
    }
}
