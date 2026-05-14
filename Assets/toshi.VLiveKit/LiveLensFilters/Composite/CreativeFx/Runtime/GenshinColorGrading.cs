using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Genshin Color Grading")]
    public sealed class GenshinColorGrading : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(1f, 0f, 5f);
        public ClampedFloatParameter stretch = new ClampedFloatParameter(0.75f, 0f, 1f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        public ColorParameter tint = new ColorParameter(new Color(0.55f, 0.55f, 1f), false, false, true);
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1f, 0f, 5f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 2f);
        public BoolParameter toneMap = new BoolParameter(false);

        Material material;

        static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int StretchId = Shader.PropertyToID("_Stretch");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int ToneMapId = Shader.PropertyToID("_ToneMap");

        public bool IsActive() => material != null && intensity.value > 0;

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public override bool visibleInSceneView => false;

        public override void Setup()
        {
            material = CoreUtils.CreateEngineMaterial("Hidden/toshi/LensFilters/GenshinColorGrading");
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle srcRT, RTHandle destRT)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                HDUtils.BlitCameraTexture(cmd, srcRT, destRT);
                return;
            }

            material.SetTexture(InputTextureId, srcRT);
            material.SetFloat(ThresholdId, threshold.value);
            material.SetFloat(StretchId, stretch.value);
            material.SetFloat(IntensityId, intensity.value);
            material.SetColor(ColorId, tint.value);
            material.SetFloat(ExposureId, exposure.value);
            material.SetFloat(ContrastId, contrast.value);
            material.SetFloat(SaturationId, saturation.value);
            material.SetFloat(ToneMapId, toneMap.value ? 1f : 0f);
            HDUtils.DrawFullScreen(cmd, material, destRT);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
        }
    }
}
