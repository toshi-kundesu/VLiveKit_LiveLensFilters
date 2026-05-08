using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Bleach Bypass")]
    public sealed class BleachBypass : CreativeFxBase
    {
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0.55f, 0, 1);

        protected override int Mode => 14;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, contrast.value);
        }
    }
}
