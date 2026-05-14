using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

[System.Serializable]
public sealed class LayerBloom : CustomPass
{
    public enum BloomColorMode
    {
        SourceColor,
        TintColor,
        SourceColorTinted
    }

    [Header("Target")]
    public LayerMask targetLayer = 1;
    public bool useCameraDepth = true;

    [Header("Bloom")]
    [Min(0f)] public float threshold = 0.75f;
    [Range(0f, 1f)] public float softKnee = 0.5f;
    [Min(0f)] public float sourceBoost = 0.8f;
    [Range(1, 4)] public int downsample = 2;
    [Range(1, 8)] public int blurIterations = 4;
    [Min(0f)] public float blurRadius = 1.25f;
    [Min(0f)] public float intensity = 0.35f;
    public BloomColorMode colorMode = BloomColorMode.SourceColorTinted;
    [ColorUsage(false, true)] public Color tint = Color.white;

    [Header("Stability")]
    [Min(0f)] public float maxSourceBrightness = 3f;
    [Min(0f)] public float maxBloomBrightness = 1.25f;

    [Header("Debug")]
    public bool showBloomOnly;

    [SerializeField, HideInInspector] Shader layerBloomShader;

    Material layerBloomMaterial;
    MaterialPropertyBlock propertyBlock;
    RTHandle layerColorTexture;
    RTHandle bloomTextureA;
    RTHandle bloomTextureB;
    RTHandle compositeTexture;
    ShaderTagId[] shaderTags;

    int prefilterPass;
    int horizontalBlurPass;
    int verticalBlurPass;
    int compositePass;
    int bloomOnlyPass;

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int MainTexTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");
    static readonly int MainTexScaleBiasId = Shader.PropertyToID("_MainTexScaleBias");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
    static readonly int SoftKneeId = Shader.PropertyToID("_SoftKnee");
    static readonly int SourceBoostId = Shader.PropertyToID("_SourceBoost");
    static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int ColorModeId = Shader.PropertyToID("_ColorMode");
    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
    static readonly int MaxSourceBrightnessId = Shader.PropertyToID("_MaxSourceBrightness");
    static readonly int MaxBloomBrightnessId = Shader.PropertyToID("_MaxBloomBrightness");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (layerBloomShader == null)
            layerBloomShader = Shader.Find("Hidden/toshi/LensFilters/CustomPass/LayerBloom");

        layerBloomMaterial = CoreUtils.CreateEngineMaterial(layerBloomShader);
        propertyBlock = new MaterialPropertyBlock();

        if (layerBloomMaterial != null)
        {
            prefilterPass = FindPassOrDefault(layerBloomMaterial, "Prefilter", 0);
            horizontalBlurPass = FindPassOrDefault(layerBloomMaterial, "Horizontal Blur", 1);
            verticalBlurPass = FindPassOrDefault(layerBloomMaterial, "Vertical Blur", 2);
            compositePass = FindPassOrDefault(layerBloomMaterial, "Composite", 3);
            bloomOnlyPass = FindPassOrDefault(layerBloomMaterial, "Bloom Only", 4);
        }

        shaderTags = new[]
        {
            new ShaderTagId("Forward"),
            new ShaderTagId("ForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("FirstPass"),
        };
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (layerBloomMaterial == null || targetLayer.value == 0)
            return;

        SyncRenderTextures(ctx);

        RenderTargetLayer(ctx);
        ApplyBloom(ctx);
    }

    void RenderTargetLayer(CustomPassContext ctx)
    {
        var previousColor = ctx.cameraColorBuffer;
        var previousDepth = ctx.cameraDepthBuffer;

        if (useCameraDepth)
            CoreUtils.SetRenderTarget(ctx.cmd, layerColorTexture, previousDepth, ClearFlag.Color, Color.clear);
        else
            CoreUtils.SetRenderTarget(ctx.cmd, layerColorTexture, ClearFlag.Color, Color.clear);

        var depthCompare = useCameraDepth ? CompareFunction.LessEqual : CompareFunction.Always;
        var rendererList = new RendererListDesc(shaderTags, ctx.cullingResults, ctx.hdCamera.camera)
        {
            rendererConfiguration = PerObjectData.None,
            renderQueueRange = RenderQueueRange.all,
            sortingCriteria = SortingCriteria.CommonTransparent,
            excludeObjectMotionVectors = false,
            layerMask = targetLayer,
            stateBlock = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(false, depthCompare)
            },
        };

        CoreUtils.DrawRendererList(ctx.cmd, ctx.renderContext.CreateRendererList(rendererList));
        CoreUtils.SetRenderTarget(ctx.cmd, previousColor, previousDepth);
    }

    void ApplyBloom(CustomPassContext ctx)
    {
        layerBloomMaterial.SetFloat(ThresholdId, threshold);
        layerBloomMaterial.SetFloat(SoftKneeId, softKnee);
        layerBloomMaterial.SetFloat(SourceBoostId, sourceBoost);
        layerBloomMaterial.SetFloat(BlurRadiusId, blurRadius);
        layerBloomMaterial.SetFloat(IntensityId, intensity);
        layerBloomMaterial.SetInt(ColorModeId, (int)colorMode);
        layerBloomMaterial.SetColor(TintId, tint);
        layerBloomMaterial.SetFloat(MaxSourceBrightnessId, maxSourceBrightness);
        layerBloomMaterial.SetFloat(MaxBloomBrightnessId, maxBloomBrightness);

        DrawFullscreenPass(ctx, layerColorTexture, bloomTextureA, prefilterPass);

        var iterations = Mathf.Max(1, blurIterations);
        for (var i = 0; i < iterations; i++)
        {
            DrawFullscreenPass(ctx, bloomTextureA, bloomTextureB, horizontalBlurPass);
            DrawFullscreenPass(ctx, bloomTextureB, bloomTextureA, verticalBlurPass);
        }

        layerBloomMaterial.SetTexture(BloomTextureId, bloomTextureA.rt);
        DrawFullscreenPass(ctx, ctx.cameraColorBuffer, compositeTexture, showBloomOnly ? bloomOnlyPass : compositePass);
        Blitter.BlitCameraTexture(ctx.cmd, compositeTexture, ctx.cameraColorBuffer);
    }

    void DrawFullscreenPass(CustomPassContext ctx, RTHandle source, RTHandle destination, int passIndex)
    {
        if (source == null || source.rt == null || destination == null)
            return;

        propertyBlock.Clear();
        propertyBlock.SetTexture(MainTexId, source.rt);
        propertyBlock.SetVector(MainTexTexelSizeId, GetTexelSize(source));
        propertyBlock.SetVector(MainTexScaleBiasId, GetScaleBias(source));
        HDUtils.DrawFullScreen(ctx.cmd, layerBloomMaterial, destination, propertyBlock, passIndex);
    }

    void SyncRenderTextures(CustomPassContext ctx)
    {
        var colorSize = GetCameraColorSize(ctx);

        var layerWidth = colorSize.x;
        var layerHeight = colorSize.y;
        if (useCameraDepth && ctx.cameraDepthBuffer != null && ctx.cameraDepthBuffer.rt != null)
        {
            layerWidth = Mathf.Max(1, ctx.cameraDepthBuffer.rt.width);
            layerHeight = Mathf.Max(1, ctx.cameraDepthBuffer.rt.height);
        }

        var scale = Mathf.Clamp(downsample, 1, 4);
        var bloomWidth = Mathf.Max(1, layerWidth / scale);
        var bloomHeight = Mathf.Max(1, layerHeight / scale);

        layerColorTexture = EnsureRTHandle(layerColorTexture, "VLiveKit Layer Bloom Source", layerWidth, layerHeight);
        bloomTextureA = EnsureRTHandle(bloomTextureA, "VLiveKit Layer Bloom A", bloomWidth, bloomHeight);
        bloomTextureB = EnsureRTHandle(bloomTextureB, "VLiveKit Layer Bloom B", bloomWidth, bloomHeight);
        compositeTexture = EnsureRTHandle(compositeTexture, "VLiveKit Layer Bloom Composite", colorSize.x, colorSize.y);
    }

    static RTHandle EnsureRTHandle(RTHandle texture, string name, int width, int height)
    {
        if (texture != null && texture.rt != null && texture.rt.width == width && texture.rt.height == height)
            return texture;

        ReleaseRTHandle(ref texture);

        return RTHandles.Alloc(
            width,
            height,
            slices: TextureXR.slices,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            dimension: TextureXR.dimension,
            useDynamicScale: false,
            name: name);
    }

    static int FindPassOrDefault(Material material, string passName, int fallback)
    {
        var pass = material.FindPass(passName);
        return pass >= 0 ? pass : fallback;
    }

    static Vector4 GetTexelSize(RTHandle texture)
    {
        var size = GetTextureSize(texture);
        var width = size.x;
        var height = size.y;
        return new Vector4(1f / width, 1f / height, width, height);
    }

    static Vector4 GetScaleBias(RTHandle texture)
    {
        if (texture != null && texture.useScaling)
            return new Vector4(texture.rtHandleProperties.rtHandleScale.x, texture.rtHandleProperties.rtHandleScale.y, 0f, 0f);

        return new Vector4(1f, 1f, 0f, 0f);
    }

    static Vector2Int GetCameraColorSize(CustomPassContext ctx)
    {
        var width = Mathf.Max(0, ctx.hdCamera.actualWidth);
        var height = Mathf.Max(0, ctx.hdCamera.actualHeight);
        if (width > 0 && height > 0)
            return new Vector2Int(width, height);

        return GetTextureSize(ctx.cameraColorBuffer);
    }

    static Vector2Int GetTextureSize(RTHandle texture)
    {
        if (texture == null)
            return new Vector2Int(1, 1);

        if (texture.useScaling)
        {
            var scaledSize = texture.GetScaledSize(texture.rtHandleProperties.currentViewportSize);
            if (scaledSize.x > 0 && scaledSize.y > 0)
                return new Vector2Int(scaledSize.x, scaledSize.y);
        }

        if (texture.rt != null)
            return new Vector2Int(Mathf.Max(1, texture.rt.width), Mathf.Max(1, texture.rt.height));

        return new Vector2Int(1, 1);
    }

    static void ReleaseRTHandle(ref RTHandle texture)
    {
        if (texture == null)
            return;

        texture.Release();
        texture = null;
    }

    public override IEnumerable<Material> RegisterMaterialForInspector()
    {
        if (layerBloomMaterial != null)
            yield return layerBloomMaterial;
    }

    protected override void Cleanup()
    {
        ReleaseRTHandle(ref layerColorTexture);
        ReleaseRTHandle(ref bloomTextureA);
        ReleaseRTHandle(ref bloomTextureB);
        ReleaseRTHandle(ref compositeTexture);
        CoreUtils.Destroy(layerBloomMaterial);
    }
}
