using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Film Grain")]
    public sealed class FilmGrain : CreativeFxBase
    {
        public ClampedFloatParameter amount = new ClampedFloatParameter(0.18f, 0, 1);

        protected override int Mode => 10;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, amount.value);
        }
    }
}
