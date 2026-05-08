using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CopyPass : CustomPass
{
    public enum BufferType
    {
        Color,
        Normal,
        Roughness,
        Depth,
        MotionVectors,
    }

    // ★追加：Depth 出力モード
    public enum DepthOutput
    {
        Raw = 0,      // デバイス深度（LoadCameraDepthの生値）
        Linear01 = 1, // near..far を 0..1 に正規化
        Eye = 2       // カメラからの線形距離（posInput.linearDepth）
    }

    public RenderTexture outputRenderTexture;

    [Header("Global Dispatch")]
    public bool setGlobalTexture = true;

    [Tooltip("Shader global texture name to dispatch color RT (ex: _MyColorTex)")]
    [SerializeField] private string globalColorTextureName = "_CopiedColorTex";
    [SerializeField] private string globalNormalTextureName = "_CopiedNormalTex";
    [SerializeField] private string globalRoughnessTextureName = "_CopiedRoughnessTex";
    [SerializeField] private string globalDepthTextureName = "_CopiedDepthTex";
    [SerializeField] private string globalMotionVectorsTextureName = "_CopiedMotionVectorsTex";

    [Header("Buffer Type")]
    public BufferType bufferType;

    [Header("Depth Output (Depth only)")]
    public DepthOutput depthOutput = DepthOutput.Eye;

    [SerializeField, HideInInspector]
    Shader customCopyShader;
    Material customCopyMaterial;

    protected override bool executeInSceneView => false;

    int normalPass;
    int roughnessPass;
    int depthPass;

    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int DepthOutputModeId = Shader.PropertyToID("_DepthOutputMode");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (customCopyShader == null)
            customCopyShader = Shader.Find("Hidden/FullScreen/CustomCopy");

        customCopyMaterial = CoreUtils.CreateEngineMaterial(customCopyShader);

        normalPass = customCopyMaterial.FindPass("Normal");
        roughnessPass = customCopyMaterial.FindPass("Roughness");
        depthPass = customCopyMaterial.FindPass("Depth");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (outputRenderTexture == null || customCopyMaterial == null)
            return;

        SyncRenderTextureAspect(outputRenderTexture, ctx.hdCamera.camera);

        var scale = RTHandles.rtHandleProperties.rtHandleScale;
        customCopyMaterial.SetVector(ScaleId, scale);

        switch (bufferType)
        {
            default:
            case BufferType.Color:
                ctx.cmd.Blit(ctx.cameraColorBuffer, outputRenderTexture,
                    new Vector2(scale.x, scale.y), Vector2.zero, 0, 0);

                if (setGlobalTexture && !string.IsNullOrEmpty(globalColorTextureName))
                    ctx.cmd.SetGlobalTexture(globalColorTextureName, outputRenderTexture);
                break;

            case BufferType.Normal:
                ctx.cmd.Blit(ctx.cameraNormalBuffer, outputRenderTexture, customCopyMaterial, normalPass);

                if (setGlobalTexture && !string.IsNullOrEmpty(globalNormalTextureName))
                    ctx.cmd.SetGlobalTexture(globalNormalTextureName, outputRenderTexture);
                break;

            case BufferType.Roughness:
                ctx.cmd.Blit(ctx.cameraNormalBuffer, outputRenderTexture, customCopyMaterial, roughnessPass);

                if (setGlobalTexture && !string.IsNullOrEmpty(globalRoughnessTextureName))
                    ctx.cmd.SetGlobalTexture(globalRoughnessTextureName, outputRenderTexture);
                break;

            case BufferType.Depth:
                // ★追加：Depth出力モード（Raw/Linear01/Eye）をShaderへ
                customCopyMaterial.SetInt(DepthOutputModeId, (int)depthOutput);

                ctx.cmd.Blit(ctx.cameraNormalBuffer, outputRenderTexture, customCopyMaterial, depthPass);

                if (setGlobalTexture && !string.IsNullOrEmpty(globalDepthTextureName))
                    ctx.cmd.SetGlobalTexture(globalDepthTextureName, outputRenderTexture);
                break;

            case BufferType.MotionVectors:
                ctx.cmd.Blit(ctx.cameraMotionVectorsBuffer, outputRenderTexture,
                    new Vector2(scale.x, scale.y), Vector2.zero, 0, 0);

                if (setGlobalTexture && !string.IsNullOrEmpty(globalMotionVectorsTextureName))
                    ctx.cmd.SetGlobalTexture(globalMotionVectorsTextureName, outputRenderTexture);
                break;
        }
    }

    void SyncRenderTextureAspect(RenderTexture rt, Camera camera)
    {
        float aspect = rt.width / (float)rt.height;

        if (!Mathf.Approximately(aspect, camera.aspect))
        {
            rt.Release();
            rt.width = camera.pixelWidth;
            rt.height = camera.pixelHeight;
            rt.Create();
        }
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(customCopyMaterial);
    }
}
