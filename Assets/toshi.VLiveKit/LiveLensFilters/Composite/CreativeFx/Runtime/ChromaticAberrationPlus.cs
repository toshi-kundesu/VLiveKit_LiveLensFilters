using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Chromatic Aberration Plus")]
    public sealed class ChromaticAberrationPlus : CreativeFxBase
    {
        public ClampedFloatParameter amount = new ClampedFloatParameter(0.5f, 0, 1);
        public ClampedFloatParameter edgeBias = new ClampedFloatParameter(0.7f, 0, 2);

        protected override int Mode => 1;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, amount.value);
            mat.SetFloat(ShaderIDs.Radius, edgeBias.value);
        }
    }
}
