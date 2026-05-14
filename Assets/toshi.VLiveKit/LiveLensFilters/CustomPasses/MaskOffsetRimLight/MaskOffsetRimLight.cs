using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

[System.Serializable]
public sealed class MaskOffsetRimLight : CustomPass
{
    [Header("Target")]
    public LayerMask targetLayer = 1;
    public bool useCameraDepth = true;

    [Header("Rim")]
    public Vector2 offsetPixels = new Vector2(8f, 0f);
    [Min(0f)] public float intensity = 1f;
    [ColorUsage(false, true)] public Color color = Color.white;

    [Header("Debug")]
    public bool showMaskOnly;
    public bool showRimOnly;

    [SerializeField, HideInInspector] Shader rimShader;

    Material rimMaterial;
    MaterialPropertyBlock propertyBlock;
    RTHandle maskTexture;
    RTHandle compositeTexture;
    ShaderTagId[] shaderTags;

    int maskPass;
    int compositePass;

    static readonly int CameraColorTextureId = Shader.PropertyToID("_CameraColorTexture");
    static readonly int CameraColorScaleBiasId = Shader.PropertyToID("_CameraColorScaleBias");
    static readonly int MaskTextureId = Shader.PropertyToID("_MaskTexture");
    static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
    static readonly int MaskScaleBiasId = Shader.PropertyToID("_MaskScaleBias");
    static readonly int OffsetPixelsId = Shader.PropertyToID("_OffsetPixels");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (rimShader == null)
            rimShader = Shader.Find("Hidden/toshi/LensFilters/CustomPass/MaskOffsetRimLight");

        rimMaterial = CoreUtils.CreateEngineMaterial(rimShader);
        propertyBlock = new MaterialPropertyBlock();

        if (rimMaterial != null)
        {
            maskPass = FindPassOrDefault(rimMaterial, "Mask", 0);
            compositePass = FindPassOrDefault(rimMaterial, "Composite", 1);
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
        if (rimMaterial == null || targetLayer.value == 0)
            return;

        SyncRenderTextures(ctx);
        RenderMask(ctx);
        CompositeRim(ctx);
    }

    void RenderMask(CustomPassContext ctx)
    {
        var previousColor = ctx.cameraColorBuffer;
        var previousDepth = ctx.cameraDepthBuffer;

        if (useCameraDepth)
            CoreUtils.SetRenderTarget(ctx.cmd, maskTexture, previousDepth, ClearFlag.Color, Color.clear);
        else
            CoreUtils.SetRenderTarget(ctx.cmd, maskTexture, ClearFlag.Color, Color.clear);

        var depthCompare = useCameraDepth ? CompareFunction.LessEqual : CompareFunction.Always;
        var rendererList = new RendererListDesc(shaderTags, ctx.cullingResults, ctx.hdCamera.camera)
        {
            rendererConfiguration = PerObjectData.None,
            renderQueueRange = RenderQueueRange.all,
            sortingCriteria = SortingCriteria.CommonTransparent,
            excludeObjectMotionVectors = false,
            layerMask = targetLayer,
            overrideMaterial = rimMaterial,
            overrideMaterialPassIndex = maskPass,
            stateBlock = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(false, depthCompare)
            },
        };

        CoreUtils.DrawRendererList(ctx.cmd, ctx.renderContext.CreateRendererList(rendererList));
        CoreUtils.SetRenderTarget(ctx.cmd, previousColor, previousDepth);
    }

    void CompositeRim(CustomPassContext ctx)
    {
        propertyBlock.Clear();
        propertyBlock.SetTexture(CameraColorTextureId, ctx.cameraColorBuffer.rt);
        propertyBlock.SetVector(CameraColorScaleBiasId, GetScaleBias(ctx.cameraColorBuffer));
        propertyBlock.SetTexture(MaskTextureId, maskTexture.rt);
        propertyBlock.SetVector(MaskTexelSizeId, GetTexelSize(maskTexture));
        propertyBlock.SetVector(MaskScaleBiasId, GetScaleBias(maskTexture));
        propertyBlock.SetVector(OffsetPixelsId, offsetPixels);
        propertyBlock.SetFloat(IntensityId, intensity);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetFloat(DebugModeId, showMaskOnly ? 1f : showRimOnly ? 2f : 0f);

        HDUtils.DrawFullScreen(ctx.cmd, rimMaterial, compositeTexture, propertyBlock, compositePass);
        Blitter.BlitCameraTexture(ctx.cmd, compositeTexture, ctx.cameraColorBuffer);
    }

    void SyncRenderTextures(CustomPassContext ctx)
    {
        var colorSize = GetCameraColorSize(ctx);
        var maskWidth = colorSize.x;
        var maskHeight = colorSize.y;

        if (useCameraDepth && ctx.cameraDepthBuffer != null && ctx.cameraDepthBuffer.rt != null)
        {
            maskWidth = Mathf.Max(1, ctx.cameraDepthBuffer.rt.width);
            maskHeight = Mathf.Max(1, ctx.cameraDepthBuffer.rt.height);
        }

        maskTexture = EnsureRTHandle(maskTexture, "VLiveKit Mask Offset Rim Mask", maskWidth, maskHeight, GraphicsFormat.R8_UNorm);
        compositeTexture = EnsureRTHandle(compositeTexture, "VLiveKit Mask Offset Rim Composite", colorSize.x, colorSize.y, GraphicsFormat.R16G16B16A16_SFloat);
    }

    static RTHandle EnsureRTHandle(RTHandle texture, string name, int width, int height, GraphicsFormat format)
    {
        if (texture != null && texture.rt != null && texture.rt.width == width && texture.rt.height == height && texture.rt.graphicsFormat == format)
            return texture;

        ReleaseRTHandle(ref texture);

        return RTHandles.Alloc(
            width,
            height,
            slices: TextureXR.slices,
            colorFormat: format,
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
        if (rimMaterial != null)
            yield return rimMaterial;
    }

    protected override void Cleanup()
    {
        ReleaseRTHandle(ref maskTexture);
        ReleaseRTHandle(ref compositeTexture);
        CoreUtils.Destroy(rimMaterial);
    }
}
