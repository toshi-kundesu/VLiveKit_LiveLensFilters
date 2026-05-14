using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Depth Fog Overlay")]
    public sealed class DepthFogOverlay : CreativeFxBase
    {
        public ColorParameter fogColor = new ColorParameter(new Color(0.55f, 0.72f, 0.78f), false, false, true);
        public MinFloatParameter near = new MinFloatParameter(6, 0);
        public MinFloatParameter far = new MinFloatParameter(35, 0.01f);

        protected override int Mode => 7;

        protected override void SetParameters(Material mat)
        {
            mat.SetColor(ShaderIDs.Color, fogColor.value);
            mat.SetFloat(ShaderIDs.Near, near.value);
            mat.SetFloat(ShaderIDs.Far, Mathf.Max(near.value + 0.01f, far.value));
        }
    }
}
