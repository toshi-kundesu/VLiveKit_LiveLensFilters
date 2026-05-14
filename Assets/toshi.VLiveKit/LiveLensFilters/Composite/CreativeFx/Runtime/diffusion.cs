using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using SerializableAttribute = System.SerializableAttribute;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/toshi/LensFilters/Diffusion")]
    public sealed class diffusion : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public enum SourceMode { FullFrame, HighlightsOnly }
        public enum BlendMode { Lighten, Screen }

        [Serializable] public sealed class SourceModeParameter : VolumeParameter<SourceMode> {}
        [Serializable] public sealed class BlendModeParameter : VolumeParameter<BlendMode> {}

        public SourceModeParameter sourceMode = new SourceModeParameter { value = SourceMode.HighlightsOnly };
        public BlendModeParameter blendMode = new BlendModeParameter { value = BlendMode.Lighten };
        public BoolParameter useTint = new BoolParameter(false);
        public ColorParameter tint = new ColorParameter(new Color(0.55f, 0.55f, 1f), false, false, true);

        public ClampedFloatParameter stretch = new ClampedFloatParameter(0.75f, 0f, 1f);
        public ClampedFloatParameter threshold = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter blurRadius = new ClampedFloatParameter(2f, 0.1f, 10f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 5f);
        public Vector4Parameter weights = new Vector4Parameter(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter bloomIntensity = new ClampedFloatParameter(1f, 0f, 10f);
        public ColorParameter bloomColor = new ColorParameter(new Color(0.55f, 0.55f, 1f), false, false, true);

        Material material;

        static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
        static readonly int StretchId = Shader.PropertyToID("_Stretch");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int BloomWeightsId = Shader.PropertyToID("_BloomWeights");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int BloomColorId = Shader.PropertyToID("_BloomColor");
        static readonly int SourceModeId = Shader.PropertyToID("_SourceMode");
        static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
        static readonly int UseTintId = Shader.PropertyToID("_UseTint");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        public bool IsActive() => material != null && intensity.value > 0;

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public override bool visibleInSceneView => false;

        public override void Setup()
        {
            material = CoreUtils.CreateEngineMaterial("Hidden/toshi/LensFilters/Diffusion");
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle srcRT, RTHandle destRT)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                HDUtils.BlitCameraTexture(cmd, srcRT, destRT);
                return;
            }

            material.SetTexture(InputTextureId, srcRT);
            material.SetFloat(StretchId, stretch.value);
            material.SetFloat(ThresholdId, threshold.value);
            material.SetFloat(BlurRadiusId, blurRadius.value);
            material.SetFloat(IntensityId, intensity.value);
            material.SetVector(BloomWeightsId, weights.value);
            material.SetFloat(ExposureId, exposure.value);
            material.SetFloat(ContrastId, contrast.value);
            material.SetFloat(SaturationId, saturation.value);
            material.SetFloat(BloomIntensityId, bloomIntensity.value);
            material.SetColor(BloomColorId, bloomColor.value);
            material.SetInt(SourceModeId, (int)sourceMode.value);
            material.SetInt(BlendModeId, (int)blendMode.value);
            material.SetInt(UseTintId, useTint.value ? 1 : 0);
            material.SetColor(TintId, tint.value);
            HDUtils.DrawFullScreen(cmd, material, destRT);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
        }
    }
}
