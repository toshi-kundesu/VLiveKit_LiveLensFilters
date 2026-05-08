using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Light Sweep")]
    public sealed class LightSweep : CreativeFxBase
    {
        public ClampedFloatParameter position = new ClampedFloatParameter(0.5f, 0, 1);
        public ClampedFloatParameter angle = new ClampedFloatParameter(0.5f, 0, 1);
        public ClampedFloatParameter width = new ClampedFloatParameter(0.28f, 0.02f, 1);
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.28f, 0, 2);
        public ClampedFloatParameter speed = new ClampedFloatParameter(0, 0, 1);
        public ColorParameter tint = new ColorParameter(new Color(1, 0.86f, 0.55f), false, false, true);

        protected override int Mode => 23;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Amount, position.value);
            mat.SetFloat(ShaderIDs.Radius, angle.value);
            mat.SetFloat(ShaderIDs.Threshold, width.value);
            mat.SetFloat(ShaderIDs.Near, threshold.value);
            mat.SetFloat(ShaderIDs.Far, speed.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
        }
    }
}
