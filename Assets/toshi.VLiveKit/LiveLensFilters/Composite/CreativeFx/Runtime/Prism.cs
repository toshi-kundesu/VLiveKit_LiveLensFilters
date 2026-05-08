using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Prism")]
    public sealed class Prism : CreativeFxBase
    {
        public ClampedFloatParameter refraction = new ClampedFloatParameter(0.45f, 0, 1);
        public ClampedFloatParameter facets = new ClampedFloatParameter(0.35f, 0, 1);

        protected override int Mode => 3;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, refraction.value);
            mat.SetFloat(ShaderIDs.Radius, facets.value);
        }
    }
}
