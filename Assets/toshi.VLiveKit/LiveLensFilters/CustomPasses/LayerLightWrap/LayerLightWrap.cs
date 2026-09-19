using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

/// <summary>Wraps visible surrounding background color into a selected character's inner silhouette.</summary>
[System.Serializable]
public sealed class LayerLightWrap : CustomPass
{
    public enum MaskSource { MaterialAlpha, SolidGeometry }
    public enum BlendMode { Screen, Additive, Lighten }
    public enum DebugView { None, TargetMask, WrapOnly, BackgroundColor, DirectLightRimOnly, DirectLighting }

    [Header("Character")]
    [Tooltip("GameObject layers receiving the wrap. Put this pass in Before Post Process.")]
    public LayerMask targetLayer = 1;
    [Tooltip("Use only Target Renderers, preserving their existing GameObject layers. An empty list targets nothing.")]
    public bool useExplicitRenderers;
    public Renderer[] targetRenderers = new Renderer[0];
    [Tooltip("Respect foreground occluders using the camera depth buffer.")]
    public bool useCameraDepth = true;
    [Tooltip("Material Alpha preserves the source shader's cutouts and vertex deformation. Use Solid Geometry for opaque shaders that do not write alpha; it ignores cutouts and shader deformation.")]
    public MaskSource maskSource = MaskSource.MaterialAlpha;

    [Header("Light Wrap")]
    [Tooltip("Reach of the background light in pixels at 1080p; scales with the camera resolution.")]
    [Range(1f, 128f)] public float width = 32f;
    [Range(0f, 1f)] public float softness = 0.6f;
    [Range(0f, 2f)] public float intensity = 0.8f;
    public BlendMode blendMode = BlendMode.Screen;
    [Tooltip("Exposure adjustment for the sampled background, in stops.")]
    [Range(-4f, 4f)] public float backgroundExposure;
    [Range(0f, 2f)] public float saturation = 1f;
    [ColorUsage(false, true)] public Color tint = Color.white;

    [Header("Scene Light Rim (LiveToon)")]
    [Tooltip("Actual scene lighting, including attenuation and shadows, masked to the character silhouette. Requires a material with the VLiveKitDirectLight pass (LiveToon). Zero disables the extra rendering.")]
    [Range(0f, 2f)] public float directLightIntensity;
    [Tooltip("Inner silhouette rim width in pixels at 1080p. Does not create edges between materials or facial features.")]
    [Range(1f, 128f)] public float directLightWidth = 12f;
    [Range(0f, 1f)] public float directLightSoftness = 0.6f;
    [Range(-4f, 4f)] public float directLightExposure;
    [ColorUsage(false, true)] public Color directLightTint = Color.white;

    [Header("Debug")]
    public DebugView debugView;

    [SerializeField, HideInInspector] public bool managedByLiveToonSetup;

    Material material;
    MaterialPropertyBlock properties;
    RTHandle mask;
    RTHandle backgroundA;
    RTHandle backgroundB;
    RTHandle composite;
    RTHandle privateDepth;
    RTHandle directLighting;
    RTHandle directDepth;
    RTHandle directRimA;
    RTHandle directRimB;
    bool missingShaderReported;
    readonly Plane[] frustumPlanes = new Plane[6];
    readonly List<Material> sharedMaterials = new List<Material>();
    static readonly string[] ForwardPassNames =
    {
        "FORWARD_BASE + ADD", "ForwardOnly", "Forward", "SRPDefaultUnlit", "ForwardLit", "FirstPass"
    };

    ShaderTagId[] shaderTags;
    ShaderTagId[] directLightTags;
    ShaderTagId[] depthTags;

    static readonly int CameraColorId = Shader.PropertyToID("_CameraColorTexture");
    static readonly int CameraScaleId = Shader.PropertyToID("_CameraColorScaleBias");
    static readonly int CameraTexelId = Shader.PropertyToID("_CameraTexelSize");
    static readonly int MaskId = Shader.PropertyToID("_MaskTexture");
    static readonly int MaskScaleId = Shader.PropertyToID("_MaskScaleBias");
    static readonly int SourceId = Shader.PropertyToID("_SourceTexture");
    static readonly int SourceScaleId = Shader.PropertyToID("_SourceScaleBias");
    static readonly int SourceTexelId = Shader.PropertyToID("_SourceTexelSize");
    static readonly int BlurId = Shader.PropertyToID("_BlurTexture");
    static readonly int BlurScaleId = Shader.PropertyToID("_BlurScaleBias");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int BackgroundGainId = Shader.PropertyToID("_BackgroundGain");
    static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
    static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");
    static readonly int DirectLightingId = Shader.PropertyToID("_DirectLightingTexture");
    static readonly int DirectLightingScaleId = Shader.PropertyToID("_DirectLightingScaleBias");
    static readonly int DirectRimMaskId = Shader.PropertyToID("_DirectRimMaskTexture");
    static readonly int DirectRimMaskScaleId = Shader.PropertyToID("_DirectRimMaskScaleBias");
    static readonly int DirectRimMaskTexelId = Shader.PropertyToID("_DirectRimMaskTexelSize");
    static readonly int DirectIntensityId = Shader.PropertyToID("_DirectLightIntensity");
    static readonly int DirectSoftnessId = Shader.PropertyToID("_DirectLightSoftness");
    static readonly int DirectGainId = Shader.PropertyToID("_DirectLightGain");
    static readonly int DirectTintId = Shader.PropertyToID("_DirectLightTint");
    static readonly int DirectRenderingLayersId = Shader.PropertyToID("_VLiveKitDirectLightRenderingLayers");
    static readonly int DirectRenderingLayerOverrideId = Shader.PropertyToID("_VLiveKitDirectLightUseRenderingLayerOverride");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        Cleanup();
        // Managed-reference deserialization can run off the main thread.
        // Resolve shader tag IDs only when HDRP sets the pass up for rendering.
        shaderTags = new[]
        {
            new ShaderTagId("Forward"), new ShaderTagId("ForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("FirstPass")
        };
        directLightTags = new[] { new ShaderTagId("VLiveKitDirectLight") };
        depthTags = new[] { new ShaderTagId("DepthForwardOnly"), new ShaderTagId("DepthOnly") };
        // A Resources shader reference also survives player shader stripping.
        var shader = Resources.Load<Shader>("VLiveKitLayerLightWrap");
        if (shader == null)
        {
            if (!missingShaderReported)
                Debug.LogError("Layer Light Wrap shader is missing. Keep the packaged Resources/VLiveKitLayerLightWrap.shader.");
            missingShaderReported = true;
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        properties = new MaterialPropertyBlock();
        mask = Allocate("Layer Light Wrap Character Mask", Vector2.one, GraphicsFormat.R8G8B8A8_UNorm);
        backgroundA = Allocate("Layer Light Wrap Background A", Vector2.one * 0.5f, GraphicsFormat.R16G16B16A16_SFloat);
        backgroundB = Allocate("Layer Light Wrap Background B", Vector2.one * 0.5f, GraphicsFormat.R16G16B16A16_SFloat);
        composite = Allocate("Layer Light Wrap Composite", Vector2.one, GraphicsFormat.R16G16B16A16_SFloat);
    }

    static RTHandle Allocate(string name, Vector2 scale, GraphicsFormat format)
    {
        return RTHandles.Alloc(scale, slices: TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: format, filterMode: FilterMode.Bilinear, wrapMode: TextureWrapMode.Clamp,
            useDynamicScale: true, name: name);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        var renderDirectLight = directLightIntensity > 0f
            || debugView == DebugView.DirectLightRimOnly || debugView == DebugView.DirectLighting;
        if (!renderDirectLight && directLighting != null)
            ReleaseDirectLightResources();
        // The zero-intensity path leaves the original camera target entirely untouched.
        if ((!useExplicitRenderers && targetLayer.value == 0)
            || (debugView == DebugView.None && ((intensity <= 0f && directLightIntensity <= 0f)
                || (useExplicitRenderers && (targetRenderers == null || targetRenderers.Length == 0)))))
            return;
        // Scene setup can release an unused managed pass without removing it.
        if (material == null)
            Setup(ctx.renderContext, ctx.cmd);
        if (material == null)
            return;

        DrawCharacterMask(ctx);
        if (renderDirectLight)
            DrawDirectLighting(ctx);
        properties.Clear();
        properties.SetTexture(CameraColorId, ctx.cameraColorBuffer.rt);
        properties.SetVector(CameraScaleId, ScaleBias(ctx.cameraColorBuffer));
        properties.SetVector(CameraTexelId, TexelSize(ctx.cameraColorBuffer));
        properties.SetTexture(MaskId, mask.rt);
        properties.SetVector(MaskScaleId, ScaleBias(mask));

        // RGB and background coverage are filtered together, then divided in the shader.
        // This excludes the character's own color without treating the missing background as black.
        if (intensity > 0f || debugView == DebugView.WrapOnly || debugView == DebugView.BackgroundColor)
        {
            HDUtils.DrawFullScreen(ctx.cmd, material, backgroundA, properties, 1);
            var halfSize = backgroundA.GetScaledSize(backgroundA.rtHandleProperties.currentViewportSize);
            var reach = Mathf.Clamp(width, 1f, 128f) * Mathf.Max(1, ctx.hdCamera.actualHeight) / 1080f;
            properties.SetFloat(WidthId, reach * halfSize.y / Mathf.Max(1, ctx.hdCamera.actualHeight));
            BindSource(backgroundA);
            HDUtils.DrawFullScreen(ctx.cmd, material, backgroundB, properties, 2);
            BindSource(backgroundB);
            HDUtils.DrawFullScreen(ctx.cmd, material, backgroundA, properties, 3);
            properties.SetTexture(BlurId, backgroundA.rt);
            properties.SetVector(BlurScaleId, ScaleBias(backgroundA));
        }
        properties.SetFloat(SoftnessId, Mathf.Clamp01(softness));
        properties.SetFloat(IntensityId, Mathf.Clamp(intensity, 0f, 2f));
        properties.SetFloat(BackgroundGainId, Mathf.Pow(2f, Mathf.Clamp(backgroundExposure, -4f, 4f)));
        properties.SetFloat(SaturationId, Mathf.Clamp(saturation, 0f, 2f));
        properties.SetColor(TintId, tint);
        properties.SetInt(BlendModeId, (int)blendMode);
        properties.SetInt(DebugModeId, (int)debugView);
        if (renderDirectLight)
        {
            // Filter coverage independently of background RGB and of the surface
            // lighting, so internal normal/material changes cannot create rims.
            HDUtils.DrawFullScreen(ctx.cmd, material, directRimA, properties, 5);
            var rimSize = directRimA.GetScaledSize(directRimA.rtHandleProperties.currentViewportSize);
            var rimReach = Mathf.Clamp(directLightWidth, 1f, 128f) * Mathf.Max(1, ctx.hdCamera.actualHeight) / 1080f;
            properties.SetFloat(WidthId, rimReach * rimSize.y / Mathf.Max(1, ctx.hdCamera.actualHeight));
            BindSource(directRimA);
            HDUtils.DrawFullScreen(ctx.cmd, material, directRimB, properties, 2);
            BindSource(directRimB);
            HDUtils.DrawFullScreen(ctx.cmd, material, directRimA, properties, 3);
            properties.SetTexture(DirectLightingId, directLighting.rt);
            properties.SetVector(DirectLightingScaleId, ScaleBias(directLighting));
            properties.SetTexture(DirectRimMaskId, directRimA.rt);
            properties.SetVector(DirectRimMaskScaleId, ScaleBias(directRimA));
            properties.SetVector(DirectRimMaskTexelId, TexelSize(directRimA));
        }
        properties.SetFloat(DirectIntensityId, Mathf.Clamp(directLightIntensity, 0f, 2f));
        properties.SetFloat(DirectSoftnessId, Mathf.Clamp01(directLightSoftness));
        properties.SetFloat(DirectGainId, Mathf.Pow(2f, Mathf.Clamp(directLightExposure, -4f, 4f)));
        properties.SetColor(DirectTintId, directLightTint);
        HDUtils.DrawFullScreen(ctx.cmd, material, composite, properties, 4);
        Blitter.BlitCameraTexture(ctx.cmd, composite, ctx.cameraColorBuffer);
    }

    void DrawDirectLighting(CustomPassContext ctx)
    {
        if (directLighting == null)
        {
            directLighting = Allocate("Layer Light Wrap Direct Lighting", Vector2.one, GraphicsFormat.R16G16B16A16_SFloat);
            directRimA = Allocate("Layer Light Wrap Direct Rim A", Vector2.one * 0.5f, GraphicsFormat.R16G16B16A16_SFloat);
            directRimB = Allocate("Layer Light Wrap Direct Rim B", Vector2.one * 0.5f, GraphicsFormat.R16G16B16A16_SFloat);
        }
        // Own depth for the dedicated material pass. Keep both camera depth and
        // stencil untouched, and retain the closest character surface per pixel.
        var sourceDepth = ctx.cameraDepthBuffer.rt;
        if (directDepth == null || directDepth.rt.depthStencilFormat != sourceDepth.depthStencilFormat
            || directDepth.rt.dimension != sourceDepth.dimension || directDepth.rt.volumeDepth != sourceDepth.volumeDepth
            || directDepth.rt.antiAliasing != sourceDepth.antiAliasing
            || directDepth.rt.width != sourceDepth.width || directDepth.rt.height != sourceDepth.height
            || directDepth.rt.useDynamicScale != sourceDepth.useDynamicScale
            || directDepth.rt.useDynamicScaleExplicit != sourceDepth.useDynamicScaleExplicit)
        {
            directDepth?.Release();
            directDepth = RTHandles.Alloc(sourceDepth.descriptor,
                FilterMode.Point, TextureWrapMode.Clamp, name: "Layer Light Wrap Direct Depth");
        }
        if (useCameraDepth)
        {
            ctx.cmd.CopyTexture(sourceDepth, directDepth.rt);
            CoreUtils.SetRenderTarget(ctx.cmd, directLighting, directDepth, ClearFlag.Color, Color.clear);
        }
        else
            CoreUtils.SetRenderTarget(ctx.cmd, directLighting, directDepth, ClearFlag.All, Color.clear);

        ctx.cmd.SetGlobalInteger(DirectRenderingLayerOverrideId, useExplicitRenderers ? 1 : 0);
        if (useExplicitRenderers)
        {
            var camera = ctx.hdCamera.camera;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            for (var stage = useCameraDepth ? 1 : 0; stage < 2; stage++)
            {
                var depthOnly = stage == 0;
                if (depthOnly)
                {
                    CoreUtils.SetRenderTarget(ctx.cmd, directDepth);
                    ctx.cmd.SetViewport(new Rect(0, 0, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight));
                }
                else
                    CoreUtils.SetRenderTarget(ctx.cmd, directLighting, directDepth);
                if (targetRenderers != null)
                    foreach (var renderer in targetRenderers)
                    {
                        if (renderer == null || !renderer.enabled || renderer.forceRenderingOff
                            || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                            || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                            || !GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                            continue;
                        // Unlike RendererList, DrawRenderer does not bind
                        // unity_RenderingLayer. Preserve this renderer's exact
                        // Light Layer mask, including an intentionally empty one.
                        ctx.cmd.SetGlobalInteger(DirectRenderingLayersId, unchecked((int)renderer.renderingLayerMask));
                        sharedMaterials.Clear();
                        renderer.GetSharedMaterials(sharedMaterials);
                        var skinned = renderer as SkinnedMeshRenderer;
                        var filter = renderer.GetComponent<MeshFilter>();
                        var mesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                        if (mesh == null)
                            continue;
                        for (var subMesh = 0; subMesh < Mathf.Min(mesh.subMeshCount, sharedMaterials.Count); subMesh++)
                        {
                            var sourceMaterial = sharedMaterials[subMesh];
                            if (sourceMaterial == null)
                                continue;
                            var passIndex = sourceMaterial.FindPass("VLiveKitDirectLight");
                            if (depthOnly)
                            {
                                // Unsupported foreground materials must occlude the
                                // light buffer rather than inherit a lit surface behind.
                                if (passIndex >= 0)
                                    continue;
                                passIndex = sourceMaterial.FindPass("DepthForwardOnly");
                                if (passIndex < 0)
                                    passIndex = sourceMaterial.FindPass("DepthOnly");
                            }
                            if (passIndex >= 0)
                                ctx.cmd.DrawRenderer(renderer, sourceMaterial, subMesh, passIndex);
                        }
                    }
            }
            sharedMaterials.Clear();
        }
        else
        {
            var settings = ctx.hdCamera.frameSettings;
            var list = new RendererListDesc(directLightTags, ctx.cullingResults, ctx.hdCamera.camera)
            {
                rendererConfiguration = HDUtils.GetRendererConfiguration(
                    settings.IsEnabled(FrameSettingsField.AdaptiveProbeVolume), settings.IsEnabled(FrameSettingsField.Shadowmask)),
                renderQueueRange = RenderQueueRange.all,
                sortingCriteria = SortingCriteria.CommonOpaque,
                excludeObjectMotionVectors = false,
                layerMask = targetLayer
            };
            if (!useCameraDepth)
            {
                CoreUtils.SetRenderTarget(ctx.cmd, directDepth);
                ctx.cmd.SetViewport(new Rect(0, 0, ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight));
                var depthList = new RendererListDesc(depthTags, ctx.cullingResults, ctx.hdCamera.camera)
                {
                    rendererConfiguration = list.rendererConfiguration,
                    renderQueueRange = RenderQueueRange.all,
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    excludeObjectMotionVectors = false,
                    layerMask = targetLayer
                };
                CoreUtils.DrawRendererList(ctx.cmd, ctx.renderContext.CreateRendererList(depthList));
                CoreUtils.SetRenderTarget(ctx.cmd, directLighting, directDepth);
            }
            CoreUtils.DrawRendererList(ctx.cmd, ctx.renderContext.CreateRendererList(list));
        }
        ctx.cmd.SetGlobalInteger(DirectRenderingLayerOverrideId, 0);
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);
    }

    void ReleaseDirectLightResources()
    {
        directLighting?.Release(); directLighting = null;
        directDepth?.Release(); directDepth = null;
        directRimA?.Release(); directRimA = null;
        directRimB?.Release(); directRimB = null;
    }

    void DrawCharacterMask(CustomPassContext ctx)
    {
        if (useExplicitRenderers)
        {
            DrawExplicitCharacterMask(ctx);
            return;
        }
        if (useCameraDepth)
            CoreUtils.SetRenderTarget(ctx.cmd, mask, ctx.cameraDepthBuffer, ClearFlag.Color, Color.clear);
        else
            CoreUtils.SetRenderTarget(ctx.cmd, mask, ClearFlag.Color, Color.clear);

        var settings = ctx.hdCamera.frameSettings;
        var list = new RendererListDesc(shaderTags, ctx.cullingResults, ctx.hdCamera.camera)
        {
            rendererConfiguration = HDUtils.GetRendererConfiguration(
                settings.IsEnabled(FrameSettingsField.AdaptiveProbeVolume), settings.IsEnabled(FrameSettingsField.Shadowmask)),
            renderQueueRange = RenderQueueRange.all,
            sortingCriteria = SortingCriteria.CommonTransparent,
            excludeObjectMotionVectors = false,
            layerMask = targetLayer,
            stateBlock = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(false, useCameraDepth ? CompareFunction.LessEqual : CompareFunction.Always)
            }
        };
        if (maskSource == MaskSource.SolidGeometry)
        {
            list.overrideMaterial = material;
            list.overrideMaterialPassIndex = 0;
        }
        CoreUtils.DrawRendererList(ctx.cmd, ctx.renderContext.CreateRendererList(list));
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);
    }

    void DrawExplicitCharacterMask(CustomPassContext ctx)
    {
        // DrawRenderer cannot override the original material's depth/stencil state.
        // Give it an owned depth attachment so character shaders can never modify
        // the camera depth or stencil while producing this mask.
        if (useCameraDepth)
        {
            var sourceDepth = ctx.cameraDepthBuffer.rt;
            if (privateDepth == null || privateDepth.rt.depthStencilFormat != sourceDepth.depthStencilFormat
                || privateDepth.rt.dimension != sourceDepth.dimension || privateDepth.rt.volumeDepth != sourceDepth.volumeDepth
                || privateDepth.rt.antiAliasing != sourceDepth.antiAliasing
                || privateDepth.rt.width != sourceDepth.width || privateDepth.rt.height != sourceDepth.height
                || privateDepth.rt.useDynamicScale != sourceDepth.useDynamicScale
                || privateDepth.rt.useDynamicScaleExplicit != sourceDepth.useDynamicScaleExplicit)
            {
                privateDepth?.Release();
                privateDepth = RTHandles.Alloc(sourceDepth.descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "Layer Light Wrap Private Depth");
            }
            ctx.cmd.CopyTexture(ctx.cameraDepthBuffer.rt, privateDepth.rt);
            CoreUtils.SetRenderTarget(ctx.cmd, mask, privateDepth, ClearFlag.Color, Color.clear);
        }
        else
        {
            // No depth attachment also supports opaque HDRP materials whose
            // Forward pass uses Equal against their normal depth prepass.
            privateDepth?.Release();
            privateDepth = null;
            CoreUtils.SetRenderTarget(ctx.cmd, mask, ClearFlag.Color, Color.clear);
        }

        var camera = ctx.hdCamera.camera;
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        if (targetRenderers != null)
        {
            foreach (var renderer in targetRenderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff
                    || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                    || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                    || !GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                    continue;

                sharedMaterials.Clear();
                renderer.GetSharedMaterials(sharedMaterials);
                var skinned = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;
                for (var subMesh = 0; subMesh < Mathf.Min(mesh.subMeshCount, sharedMaterials.Count); subMesh++)
                {
                    if (maskSource == MaskSource.SolidGeometry)
                        ctx.cmd.DrawRenderer(renderer, material, subMesh, 0);
                    else
                    {
                        var sourceMaterial = sharedMaterials[subMesh];
                        if (sourceMaterial == null)
                            continue;
                        foreach (var passName in ForwardPassNames)
                        {
                            var passIndex = sourceMaterial.FindPass(passName);
                            if (passIndex < 0)
                                continue;
                            ctx.cmd.DrawRenderer(renderer, sourceMaterial, subMesh, passIndex);
                            break;
                        }
                    }
                }
            }
        }
        sharedMaterials.Clear();
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);
    }

    void BindSource(RTHandle source)
    {
        properties.SetTexture(SourceId, source.rt);
        properties.SetVector(SourceScaleId, ScaleBias(source));
        properties.SetVector(SourceTexelId, TexelSize(source));
    }

    static Vector4 ScaleBias(RTHandle handle)
    {
        if (!handle.useScaling)
            return new Vector4(1f, 1f, 0f, 0f);
        if (handle.rt.useDynamicScale && DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled())
        {
            // HDRP accounts for hardware-scaled allocation rounding in this ratio.
            var scale = handle.rtHandleProperties.rtHandleScale;
            return new Vector4(scale.x, scale.y, 0f, 0f);
        }
        // Account for odd viewport dimensions on the half-resolution buffers.
        var size = handle.GetScaledSize(handle.rtHandleProperties.currentViewportSize);
        return new Vector4((float)size.x / handle.rt.width, (float)size.y / handle.rt.height, 0f, 0f);
    }

    static Vector4 TexelSize(RTHandle handle)
    {
        var size = handle.useScaling ? handle.GetScaledSize(handle.rtHandleProperties.currentViewportSize)
            : new Vector2Int(handle.rt.width, handle.rt.height);
        var width = Mathf.Max(1, size.x);
        var height = Mathf.Max(1, size.y);
        return new Vector4(1f / width, 1f / height, width, height);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(material);
        material = null;
        mask?.Release(); mask = null;
        backgroundA?.Release(); backgroundA = null;
        backgroundB?.Release(); backgroundB = null;
        composite?.Release(); composite = null;
        privateDepth?.Release(); privateDepth = null;
        ReleaseDirectLightResources();
        sharedMaterials.Clear();
    }

    public void ReleaseManagedResources()
    {
        Cleanup();
    }
}
