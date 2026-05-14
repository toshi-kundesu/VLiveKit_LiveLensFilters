using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Anime Speed Lines")]
    public sealed class AnimeSpeedLines : CreativeFxBase
    {
        public ClampedFloatParameter density = new ClampedFloatParameter(0.55f, 0, 1);
        public ClampedFloatParameter innerRadius = new ClampedFloatParameter(0.22f, 0, 1);
        public ClampedFloatParameter outerRadius = new ClampedFloatParameter(0.92f, 0, 1.5f);
        public ColorParameter color = new ColorParameter(Color.white, false, false, true);

        protected override int Mode => 17;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, density.value);
            mat.SetFloat(ShaderIDs.Near, innerRadius.value);
            mat.SetFloat(ShaderIDs.Far, outerRadius.value);
            mat.SetColor(ShaderIDs.Color, color.value);
        }
    }
}
