using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        public SourceModeParameter sourceMode = new SourceModeParameter { value = SourceMode.FullFrame };
        public BlendModeParameter blendMode = new BlendModeParameter { value = BlendMode.Screen };
        public BoolParameter useTint = new BoolParameter(false);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        public ClampedFloatParameter stretch = new ClampedFloatParameter(0.75f, 0f, 1f);
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.42f, 0f, 10f);
        public ClampedFloatParameter blurRadius = new ClampedFloatParameter(4f, 0.1f, 10f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 5f);
        // Preserve legacy profile data; a normalized Gaussian replaces the sparse kernel.
        [HideInInspector] public Vector4Parameter weights = new Vector4Parameter(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter bloomIntensity = new ClampedFloatParameter(1.25f, 0f, 10f);
        public ColorParameter bloomColor = new ColorParameter(Color.white, false, false, true);

        const float ReferenceRenderHeight = 1080f;
        const int MaxBlurPassCount = 8;

        Material material;
        RTHandle blurTextureA;
        RTHandle blurTextureB;
        MaterialPropertyBlock horizontalProperties;
        MaterialPropertyBlock blurProperties;
        MaterialPropertyBlock compositeProperties;

        static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
        static readonly int StretchId = Shader.PropertyToID("_Stretch");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int BloomColorId = Shader.PropertyToID("_BloomColor");
        static readonly int SourceModeId = Shader.PropertyToID("_SourceMode");
        static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
        static readonly int UseTintId = Shader.PropertyToID("_UseTint");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        static readonly int BlurTextureId = Shader.PropertyToID("_BlurTexture");
        static readonly int SourceTextureId = Shader.PropertyToID("_SourceTexture");
        static readonly int MinimumBlurId = Shader.PropertyToID("_MinimumBlur");

        public bool IsActive() => material != null && intensity.value > 0;

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public override bool visibleInSceneView => false;

        public override void Setup()
        {
            Cleanup();
            material = CoreUtils.CreateEngineMaterial("Hidden/toshi/LensFilters/Diffusion");
            horizontalProperties = new MaterialPropertyBlock();
            blurProperties = new MaterialPropertyBlock();
            compositeProperties = new MaterialPropertyBlock();
            blurTextureA = AllocateBlurTexture("Diffusion Blur A");
            blurTextureB = AllocateBlurTexture("Diffusion Blur B");
        }

        static RTHandle AllocateBlurTexture(string name)
        {
            return RTHandles.Alloc(
                Vector2.one,
                format: GraphicsFormat.R16G16B16A16_SFloat,
                slices: TextureXR.slices,
                filterMode: FilterMode.Bilinear,
                wrapMode: TextureWrapMode.Clamp,
                dimension: TextureXR.dimension,
                useDynamicScale: true,
                name: name);
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle srcRT, RTHandle destRT)
        {
            if (material == null || material.shader == null || !material.shader.isSupported
                || blurTextureA == null || blurTextureB == null)
            {
                HDUtils.BlitCameraTexture(cmd, srcRT, destRT);
                return;
            }

            var height = camera != null && camera.actualHeight > 0 ? camera.actualHeight : ReferenceRenderHeight;
            var radius = Mathf.Max(0f, blurRadius.value) * height / ReferenceRenderHeight;
            var initialRadiusLimit = 1f / Mathf.Lerp(1f, 1.65f, stretch.value);
            var variance = 1f;
            var lastVariance = 1f;
            var passCount = 1;
            // Begin with adjacent texels, then widen the already smooth image. Scaling
            // one sparse kernel directly produces separated copies of bright details.
            while (passCount < MaxBlurPassCount && radius * radius > initialRadiusLimit * initialRadiusLimit * variance)
            {
                lastVariance *= 4f;
                variance += lastVariance;
                passCount++;
            }
            var passRadius = radius / Mathf.Sqrt(variance);

            horizontalProperties.Clear();
            horizontalProperties.SetTexture(InputTextureId, srcRT);
            horizontalProperties.SetFloat(StretchId, stretch.value);
            horizontalProperties.SetFloat(ThresholdId, threshold.value);
            horizontalProperties.SetFloat(BlurRadiusId, passRadius);
            horizontalProperties.SetInt(SourceModeId, (int)sourceMode.value);
            horizontalProperties.SetFloat(MinimumBlurId, passCount > 1 ? 1f : 0f);
            HDUtils.DrawFullScreen(cmd, material, blurTextureA, horizontalProperties, 0);

            for (var pass = 1; pass < passCount; pass++)
            {
                blurProperties.Clear();
                blurProperties.SetTexture(BlurTextureId, blurTextureA);
                blurProperties.SetFloat(StretchId, stretch.value);
                blurProperties.SetFloat(BlurRadiusId, passRadius);
                blurProperties.SetFloat(MinimumBlurId, 1f);
                HDUtils.DrawFullScreen(cmd, material, blurTextureB, blurProperties, 1);

                passRadius *= 2f;
                blurProperties.SetTexture(BlurTextureId, blurTextureB);
                blurProperties.SetFloat(BlurRadiusId, passRadius);
                HDUtils.DrawFullScreen(cmd, material, blurTextureA, blurProperties, 2);
            }

            compositeProperties.Clear();
            compositeProperties.SetTexture(SourceTextureId, srcRT);
            compositeProperties.SetTexture(BlurTextureId, blurTextureA);
            compositeProperties.SetFloat(BlurRadiusId, passRadius);
            compositeProperties.SetFloat(MinimumBlurId, passCount > 1 ? 1f : 0f);
            compositeProperties.SetFloat(StretchId, stretch.value);
            compositeProperties.SetFloat(IntensityId, intensity.value);
            compositeProperties.SetFloat(ExposureId, exposure.value);
            compositeProperties.SetFloat(ContrastId, contrast.value);
            compositeProperties.SetFloat(SaturationId, saturation.value);
            compositeProperties.SetFloat(BloomIntensityId, bloomIntensity.value);
            compositeProperties.SetColor(BloomColorId, bloomColor.value);
            compositeProperties.SetInt(BlendModeId, (int)blendMode.value);
            compositeProperties.SetInt(UseTintId, useTint.value ? 1 : 0);
            compositeProperties.SetColor(TintId, tint.value);
            HDUtils.DrawFullScreen(cmd, material, destRT, compositeProperties, 3);
        }

        public override void Cleanup()
        {
            blurTextureA?.Release();
            blurTextureA = null;
            blurTextureB?.Release();
            blurTextureB = null;
            CoreUtils.Destroy(material);
            material = null;
            horizontalProperties = null;
            blurProperties = null;
            compositeProperties = null;
        }
    }
}
