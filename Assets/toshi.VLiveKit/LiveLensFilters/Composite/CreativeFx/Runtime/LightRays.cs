using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/VLiveKit/Light Rays")]
    public sealed class LightRays : CreativeFxBase
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.72f, 0, 4);
        public ClampedFloatParameter decay = new ClampedFloatParameter(0.58f, 0, 1);
        public ClampedFloatParameter length = new ClampedFloatParameter(0.64f, 0, 1);
        public ClampedIntParameter samples = new ClampedIntParameter(12, 2, 16);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.35f));
        public ColorParameter tint = new ColorParameter(new Color(1, 0.92f, 0.68f), false, false, true);

        protected override int Mode => 24;

        protected override void SetParameters(Material mat)
        {
            mat.SetFloat(ShaderIDs.Threshold, threshold.value);
            mat.SetFloat(ShaderIDs.Amount, decay.value);
            mat.SetFloat(ShaderIDs.Radius, length.value);
            mat.SetInt(ShaderIDs.Steps, samples.value);
            mat.SetColor(ShaderIDs.Color, tint.value);
            mat.SetColor(ShaderIDs.Color2, new Color(center.value.x, center.value.y, 0, 0));
        }
    }
}
