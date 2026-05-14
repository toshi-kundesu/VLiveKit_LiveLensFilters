using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Three Strip Color")]
    public sealed class ThreeStripColor : CreativeFxBase
    {
        public ClampedFloatParameter density = new ClampedFloatParameter(0.35f, 0, 1);

        protected override int Mode => 15;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, density.value);
        }
    }
}
