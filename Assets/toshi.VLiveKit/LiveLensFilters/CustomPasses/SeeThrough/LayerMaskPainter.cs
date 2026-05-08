using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

class LayerMaskPainter : CustomPass
{
    [Header("Target")]
    public LayerMask seeThroughLayer = 1;
    public Material seeThroughMaterial = null;

    [Header("Stencil Settings")]
    public UserStencilUsage stencilBit = UserStencilUsage.UserBit0;

    [SerializeField, HideInInspector]
    private Shader stencilShader;

    private Material stencilMaterial;
    private ShaderTagId[] shaderTags;

    // あなたの SeeThroughStencil が Ref 255 で書いてる前提
    private const int StencilRefValue = 255;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (stencilShader == null)
            stencilShader = Shader.Find("Hidden/Renderers/SeeThroughStencil");

        stencilMaterial = CoreUtils.CreateEngineMaterial(stencilShader);

        shaderTags = new ShaderTagId[4]
        {
            new ShaderTagId("Forward"),
            new ShaderTagId("ForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("FirstPass"),
        };
    }

    protected override void Execute(CustomPassContext ctx)
    {
        int stencilMask = (int)stencilBit;

        // 1) 対象を指定ビットにマーキング（ZTest: LessEqual）
        stencilMaterial.SetInt("_StencilWriteMask", stencilMask);

        RenderObjects(
            ctx.renderContext, ctx.cmd,
            stencilMaterial, 0,
            CompareFunction.LessEqual,
            ctx.cullingResults, ctx.hdCamera,
            seeThroughLayer
        );

        // 2) 壁の裏にある部分だけ描画（ZTest: GreaterEqual + Stencil Equal）
        if (seeThroughMaterial != null)
        {
            var seeThroughStencil = new StencilState(
                enabled: true,
                readMask: (byte)stencilMask,
                writeMask: 0,
                compareFunction: CompareFunction.Equal,
                passOperation: StencilOp.Keep,
                failOperation: StencilOp.Keep,
                zFailOperation: StencilOp.Keep
            );

            RenderObjects(
                ctx.renderContext, ctx.cmd,
                seeThroughMaterial,
                seeThroughMaterial.FindPass("ForwardOnly"),
                CompareFunction.GreaterEqual,
                ctx.cullingResults, ctx.hdCamera,
                seeThroughLayer,
                overrideStencil: seeThroughStencil,
                stencilRef: StencilRefValue
            );
        }
    }

    public override IEnumerable<Material> RegisterMaterialForInspector()
    {
        if (seeThroughMaterial != null) yield return seeThroughMaterial;
    }

    void RenderObjects(
        ScriptableRenderContext renderContext,
        CommandBuffer cmd,
        Material overrideMaterial,
        int passIndex,
        CompareFunction depthCompare,
        CullingResults cullingResult,
        HDCamera hdCamera,
        LayerMask layerMask,
        StencilState? overrideStencil = null,
        int stencilRef = 0
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
            layerMask = layerMask,
            stateBlock = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(true, depthCompare)
            },
        };

        if (overrideStencil.HasValue)
        {
            var block = desc.stateBlock.Value;
            block.mask |= RenderStateMask.Stencil;
            block.stencilState = overrideStencil.Value;
            block.stencilReference = stencilRef; // ★重要
            desc.stateBlock = block;
        }

        CoreUtils.DrawRendererList(renderContext, cmd, renderContext.CreateRendererList(desc));
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(stencilMaterial);
    }
}
