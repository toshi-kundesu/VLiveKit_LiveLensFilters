using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Lens Vignette")]
    public sealed class LensVignette : CreativeFxBase
    {
        public ClampedFloatParameter roundness = new ClampedFloatParameter(0.55f, 0, 1.5f);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0.35f, 0.01f, 1);
        public ColorParameter tint = new ColorParameter(new Color(0, 0, 0, 1), false, false, true);

        protected override int Mode => 11;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Radius, roundness.value);
            mat.SetFloat(ShaderIDs.Amount, softness.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
