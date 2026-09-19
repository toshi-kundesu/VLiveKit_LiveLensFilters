using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace VLiveKit.LiveLensFilters.PostProcessing
{
    public abstract class CreativeFxBase : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);

        protected Material material;

        // The passes consume the pyramid immediately, so cameras/effects can share the
        // same working set. In particular, cycling presets must not retain one full
        // pyramid for every volume component that has been shown.
        const int BlurLevelCount = 5;
        static readonly RTHandle[] blurPyramid = new RTHandle[BlurLevelCount];
        static readonly RTHandle[] blurScratch = new RTHandle[BlurLevelCount];
        static readonly int[] blurTextureIds = MakeBlurPropertyIds("_BlurPyramid");
        static readonly int[] blurGeometryIds = MakeBlurPropertyIds("_BlurGeometry");
        static readonly int BlurSourceId = Shader.PropertyToID("_BlurSourceTexture");
        static readonly int BlurSourceGeometryId = Shader.PropertyToID("_BlurSourceGeometry");
        static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        static MaterialPropertyBlock blurProperties;
        static int materialOwnerCount;
        bool ownsMaterial;

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
            if (ownsMaterial)
                return;
            material = CoreUtils.CreateEngineMaterial("Hidden/toshi/LensFilters/CreativeFx");
            ownsMaterial = true;
            materialOwnerCount++;
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
            if (UsesFilteredInput(Mode))
                BuildBlurPyramid(cmd, srcRT);
            HDUtils.DrawFullScreen(cmd, material, destRT);
        }

        static bool UsesFilteredInput(int mode) =>
            mode == 0 || mode == 5 || mode == 6 || mode == 13 || mode == 16 ||
            mode == 21 || mode == 24 || mode == 25 || mode == 26 || mode == 27 || mode == 29;

        static int[] MakeBlurPropertyIds(string prefix)
        {
            var ids = new int[BlurLevelCount];
            for (var level = 0; level < ids.Length; level++)
                ids[level] = Shader.PropertyToID(prefix + level);
            return ids;
        }

        static void EnsureBlurTextures()
        {
            if (blurPyramid[0] != null)
                return;
            blurProperties = new MaterialPropertyBlock();
            for (var level = 0; level < BlurLevelCount; level++)
            {
                var scale = Vector2.one / (1 << level);
                blurPyramid[level] = AllocateBlurTexture(scale, "CreativeFx Blur " + level);
                blurScratch[level] = AllocateBlurTexture(scale, "CreativeFx Blur Scratch " + level);
            }
        }

        static RTHandle AllocateBlurTexture(Vector2 scale, string name) => RTHandles.Alloc(
            size => new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(size.x * scale.x)),
                Mathf.Max(1, Mathf.RoundToInt(size.y * scale.y))),
            slices: TextureXR.slices,
            dimension: TextureXR.dimension,
            colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            // The valid viewport already follows HDRP's post-process resolution.
            // Keep the backing allocation unscaled so hardware dynamic resolution
            // cannot apply a second scale to the explicit geometry below.
            useDynamicScale: false,
            name: name);

        void BuildBlurPyramid(CommandBuffer cmd, RTHandle source)
        {
            EnsureBlurTextures();
            var properties = source.rtHandleProperties;
            var viewport = properties.currentViewportSize;
            for (var level = 0; level < BlurLevelCount; level++)
            {
                // HDRP supplies the post-process viewport, which can differ from the
                // camera render viewport when an upscaler is enabled.
                blurPyramid[level].SetCustomHandleProperties(properties);
                blurScratch[level].SetCustomHandleProperties(properties);
                var levelViewport = blurPyramid[level].GetScaledSize(viewport);
                var geometry = GetBlurGeometry(blurPyramid[level], levelViewport);

                if (level == 0)
                {
                    var sourceGeometry = new Vector4(
                        properties.rtHandleScale.x, properties.rtHandleScale.y,
                        1f / Mathf.Max(1, viewport.x), 1f / Mathf.Max(1, viewport.y));
                    DrawBlurPass(cmd, source, sourceGeometry, blurScratch[level],
                        new Vector2(sourceGeometry.z, 0), Mode == 27 ? 3 : 2);
                }
                else
                {
                    var previousViewport = blurPyramid[level - 1].GetScaledSize(viewport);
                    DrawBlurPass(cmd, blurPyramid[level - 1],
                        GetBlurGeometry(blurPyramid[level - 1], previousViewport),
                        blurPyramid[level], Vector2.zero, 1);
                    DrawBlurPass(cmd, blurPyramid[level], geometry, blurScratch[level],
                        new Vector2(geometry.z, 0), 2);
                }

                DrawBlurPass(cmd, blurScratch[level], geometry, blurPyramid[level],
                    new Vector2(0, geometry.w), 2);
                material.SetTexture(blurTextureIds[level], blurPyramid[level]);
                material.SetVector(blurGeometryIds[level], geometry);
                blurPyramid[level].ClearCustomHandleProperties();
                blurScratch[level].ClearCustomHandleProperties();
            }
        }

        static Vector4 GetBlurGeometry(RTHandle texture, Vector2Int viewport) => new Vector4(
            (float)Mathf.Max(1, viewport.x) / Mathf.Max(1, texture.rt.width),
            (float)Mathf.Max(1, viewport.y) / Mathf.Max(1, texture.rt.height),
            1f / Mathf.Max(1, viewport.x), 1f / Mathf.Max(1, viewport.y));

        void DrawBlurPass(CommandBuffer cmd, RTHandle source, Vector4 geometry,
            RTHandle destination, Vector2 direction, int pass)
        {
            blurProperties.Clear();
            blurProperties.SetTexture(BlurSourceId, source);
            blurProperties.SetVector(BlurSourceGeometryId, geometry);
            blurProperties.SetVector(BlurDirectionId, direction);
            HDUtils.DrawFullScreen(cmd, material, destination, blurProperties, pass);
        }

        protected virtual void SetParameters(Material mat) {}

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
            material = null;
            if (!ownsMaterial)
                return;
            ownsMaterial = false;
            if (--materialOwnerCount != 0)
                return;
            for (var level = 0; level < BlurLevelCount; level++)
            {
                blurPyramid[level]?.Release();
                blurScratch[level]?.Release();
                blurPyramid[level] = null;
                blurScratch[level] = null;
            }
            blurProperties = null;
        }
    }
}
