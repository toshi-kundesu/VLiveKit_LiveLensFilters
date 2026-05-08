using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;
using System.Collections.Generic;

class SeeThrough : CustomPass
{
    [Header("Target")]
    public LayerMask seeThroughLayer = 1;
    public Material seeThroughMaterial = null;

    [Header("Stencil Settings")]
    public UserStencilUsage stencilBit = UserStencilUsage.UserBit0;

    [Header("Output / Global Dispatch")]
    public RenderTexture outputRenderTexture;
    public bool setGlobalTexture = true;
    [SerializeField] private string globalTextureName = "_SeeThroughFinalTex";

    [SerializeField, HideInInspector] Shader stencilShader;

    Material stencilMaterial;
    ShaderTagId[] shaderTags;

    static readonly int StencilWriteMaskId = Shader.PropertyToID("_StencilWriteMask");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (stencilShader == null)
            stencilShader = Shader.Find("Hidden/Renderers/SeeThroughStencil");

        stencilMaterial = CoreUtils.CreateEngineMaterial(stencilShader);

        shaderTags = new ShaderTagId[]
        {
            new ShaderTagId("Forward"),
            new ShaderTagId("ForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("FirstPass"),
        };
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (outputRenderTexture == null || seeThroughMaterial == null || stencilMaterial == null)
            return;

        SyncRenderTextureToCamera(outputRenderTexture, ctx.hdCamera.camera);

        var prevColor = ctx.cameraColorBuffer;
        var prevDepth = ctx.cameraDepthBuffer;

        // 出力先：outputRT(色) + cameraDepth(深度/ステンシル)
        ctx.cmd.SetRenderTarget(new RenderTargetIdentifier(outputRenderTexture), prevDepth.nameID);

        // ★背景は常に黒
        ctx.cmd.ClearRenderTarget(clearDepth: false, clearColor: true, backgroundColor: Color.black);

        int stencilMask = (int)stencilBit;

        // ------------------------------------------------------------
        // ① ステンシルにマーク：色は出さない（ColorMask 0）
        // ------------------------------------------------------------
        stencilMaterial.SetInt(StencilWriteMaskId, stencilMask);

        RenderObjects(
            ctx.renderContext,
            ctx.cmd,
            stencilMaterial,
            0,
            CompareFunction.LessEqual,
            ctx.cullingResults,
            ctx.hdCamera,
            overrideStencil: null,
            forceColorMask0: true   // ★ここ重要
        );

        // ------------------------------------------------------------
        // ② ステンシル一致 + ZTest >= で SeeThrough 描画（ここだけ色を描く）
        // ------------------------------------------------------------
        StencilState seeThroughStencil = new StencilState(
            enabled: true,
            readMask: (byte)stencilMask,
            compareFunction: CompareFunction.Equal
        );

        int pass = seeThroughMaterial.FindPass("ForwardOnly");
        if (pass < 0) pass = 0;

        RenderObjects(
            ctx.renderContext,
            ctx.cmd,
            seeThroughMaterial,
            pass,
            CompareFunction.GreaterEqual,
            ctx.cullingResults,
            ctx.hdCamera,
            seeThroughStencil,
            forceColorMask0: false
        );

        // グローバル配布
        if (setGlobalTexture && !string.IsNullOrEmpty(globalTextureName))
            ctx.cmd.SetGlobalTexture(globalTextureName, outputRenderTexture);

        // ★ターゲットを戻す
        CoreUtils.SetRenderTarget(ctx.cmd, prevColor, prevDepth);
    }

    public override IEnumerable<Material> RegisterMaterialForInspector()
    {
        yield return seeThroughMaterial;
    }

    void RenderObjects(
        ScriptableRenderContext renderContext,
        CommandBuffer cmd,
        Material overrideMaterial,
        int passIndex,
        CompareFunction depthCompare,
        CullingResults cullingResult,
        HDCamera hdCamera,
        StencilState? overrideStencil = null,
        bool forceColorMask0 = false
    )
    {
        var desc = new RendererListDesc(shaderTags, cullingResult, hdCamera.camera)
        {
            rendererConfiguration = PerObjectData.None,
            renderQueueRange = RenderQueueRange.all,
            sortingCriteria = SortingCriteria.BackToFront,
            excludeObjectMotionVectors = false,
            overrideMaterial = overrideMaterial,
            overrideMaterialPassIndex = passIndex,
            layerMask = seeThroughLayer,
        };

        // Depth設定
        var stateMask = RenderStateMask.Depth;
        var stateBlock = new RenderStateBlock(stateMask)
        {
            depthState = new DepthState(true, depthCompare),
        };

        // Stencil設定
        if (overrideStencil.HasValue)
        {
            stateBlock.mask |= RenderStateMask.Stencil;
            stateBlock.stencilState = overrideStencil.Value;
        }

        // ★色を一切書かない（Stencil書き込みパス用）
        if (forceColorMask0)
        {
            stateBlock.mask |= RenderStateMask.Blend;
            stateBlock.blendState = new BlendState
            {
                blendState0 = new RenderTargetBlendState
                {
                    writeMask = (ColorWriteMask)0

                }
            };
        }

        desc.stateBlock = stateBlock;

        CoreUtils.DrawRendererList(renderContext, cmd, renderContext.CreateRendererList(desc));
    }

    static void SyncRenderTextureToCamera(RenderTexture rt, Camera camera)
    {
        if (rt.width != camera.pixelWidth || rt.height != camera.pixelHeight)
        {
            rt.Release();
            rt.width = camera.pixelWidth;
            rt.height = camera.pixelHeight;
            rt.Create();
        }
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(stencilMaterial);
    }
}
