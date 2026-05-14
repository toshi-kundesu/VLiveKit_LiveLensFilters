using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    public abstract class CreativeFxBase : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);

        protected Material material;

        protected abstract int Mode { get; }

        protected static class ShaderIDs
        {
            internal static readonly int InputTexture = Shader.PropertyToID("_InputTexture");
            internal static readonly int Mode = Shader.PropertyToID("_Mode");
            internal static readonly int Intensity = Shader.PropertyToID("_Intensity");
            internal static readonly int Amount = Shader.PropertyToID("_Amount");
            internal static readonly int Threshold = Shader.PropertyToID("_Threshold");
            internal static readonly int Radius = Shader.PropertyToID("_Radius");
            internal static readonly int Color = Shader.PropertyToID("_Color");
            internal static readonly int Color2 = Shader.PropertyToID("_Color2");
            internal static readonly int Time = Shader.PropertyToID("_TimeValue");
            internal static readonly int Steps = Shader.PropertyToID("_Steps");
            internal static readonly int Near = Shader.PropertyToID("_Near");
            internal static readonly int Far = Shader.PropertyToID("_Far");
            internal static readonly int Pattern = Shader.PropertyToID("_Pattern");
            internal static readonly int PatternTexture = Shader.PropertyToID("_PatternTexture");
            internal static readonly int UsePatternTexture = Shader.PropertyToID("_UsePatternTexture");
            internal static readonly int Softness = Shader.PropertyToID("_Softness");
            internal static readonly int Rotation = Shader.PropertyToID("_Rotation");
            internal static readonly int Offset = Shader.PropertyToID("_Offset");
            internal static readonly int Pivot = Shader.PropertyToID("_Pivot");
            internal static readonly int Zoom = Shader.PropertyToID("_Zoom");
        }

        public bool IsActive() => material != null && intensity.value > 0;

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public override bool visibleInSceneView => false;

        public override void Setup()
        {
            material = CoreUtils.CreateEngineMaterial("Hidden/toshi/LensFilters/CreativeFx");
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle srcRT, RTHandle destRT)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                HDUtils.BlitCameraTexture(cmd, srcRT, destRT);
                return;
            }

            if (camera.camera.cameraType == CameraType.SceneView ||
                camera.camera.cameraType == CameraType.Preview)
            {
                HDUtils.BlitCameraTexture(cmd, srcRT, destRT);
                return;
            }

            material.SetTexture(ShaderIDs.InputTexture, srcRT);
            material.SetInt(ShaderIDs.Mode, Mode);
            material.SetFloat(ShaderIDs.Intensity, intensity.value);
            material.SetFloat(ShaderIDs.Time, Time.time);
            SetParameters(material);
            HDUtils.DrawFullScreen(cmd, material, destRT);
        }

        protected virtual void SetParameters(Material mat) {}

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
        }
    }
}
