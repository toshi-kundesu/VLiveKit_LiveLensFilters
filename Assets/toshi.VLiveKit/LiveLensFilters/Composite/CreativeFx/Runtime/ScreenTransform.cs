using UnityEngine;
using UnityEngine.Rendering;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Screen Transform")]
    public sealed class ScreenTransform : CreativeFxBase
    {
        public Vector2Parameter offset = new Vector2Parameter(Vector2.zero);
        public ClampedFloatParameter zoom = new ClampedFloatParameter(1f, 0.01f, 4f);
        public ClampedFloatParameter rotation = new ClampedFloatParameter(0f, -180f, 180f);
        public Vector2Parameter pivot = new Vector2Parameter(new Vector2(0.5f, 0.5f));

        protected override int Mode => 28;

        protected override void SetParameters(Material mat)
        {
            mat.SetVector(ShaderIDs.Offset, new Vector4(offset.value.x, offset.value.y, 0f, 0f));
            mat.SetVector(ShaderIDs.Pivot, new Vector4(pivot.value.x, pivot.value.y, 0f, 0f));
            mat.SetFloat(ShaderIDs.Zoom, zoom.value);
            mat.SetFloat(ShaderIDs.Rotation, rotation.value);
        }
    }
}
