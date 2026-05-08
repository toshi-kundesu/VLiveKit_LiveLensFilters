using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Cinema Scope")]
    public sealed class CinemaScope : CreativeFxBase
    {
        public ClampedFloatParameter aspect = new ClampedFloatParameter(0.6f, 0, 1);
        public ClampedFloatParameter extraCrop = new ClampedFloatParameter(0, 0, 1);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0.08f, 0, 1);
        public ColorParameter matteColor = new ColorParameter(Color.black, false, false, true);
        public ColorParameter centerGrade = new ColorParameter(new Color(1, 0.96f, 0.9f, 0), false, true, true);
        public ClampedFloatParameter centerGradeAmount = new ClampedFloatParameter(0, 0, 1);

        protected override int Mode => 22;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, aspect.value);
            mat.SetFloat(ShaderIDs.Radius, extraCrop.value);
            mat.SetFloat(ShaderIDs.Threshold, softness.value);
            mat.SetColor(ShaderIDs.Color, matteColor.value);
            mat.SetColor(ShaderIDs.Color2, centerGrade.value);
            mat.SetFloat(ShaderIDs.Far, centerGradeAmount.value);
        }
    }
}
