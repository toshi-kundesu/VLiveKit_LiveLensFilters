Shader "Hidden/toshi/LensFilters/CustomPass/MaskOffsetRimLight"
{
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

    struct FullscreenAttributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct FullscreenVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    struct MeshAttributes
    {
        float3 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct MeshVaryings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    FullscreenVaryings VertFullscreen(FullscreenAttributes input)
    {
        FullscreenVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    MeshVaryings VertMask(MeshAttributes input)
    {
        MeshVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS);
        return output;
    }

    TEXTURE2D_X(_CameraColorTexture);
    TEXTURE2D_X(_MaskTexture);

    float4 _CameraColorScaleBias;
    float4 _MaskTexelSize;
    float4 _MaskScaleBias;
    float2 _OffsetPixels;
    float _Intensity;
    float _DebugMode;
    int _RimPlacement;
    float4 _Color;

    float2 CameraUv(float2 uv)
    {
        return uv * _CameraColorScaleBias.xy + _CameraColorScaleBias.zw;
    }

    float2 MaskUv(float2 uv)
    {
        return uv * _MaskScaleBias.xy + _MaskScaleBias.zw;
    }

    float SampleMask(float2 uv)
    {
        float4 mask = SAMPLE_TEXTURE2D_X(_MaskTexture, s_linear_clamp_sampler, uv);
        float luma = max(mask.r, max(mask.g, mask.b));
        return saturate(max(mask.a, luma));
    }

    float BuildOutsideRim(float2 maskUv, float2 offsetUv, float original)
    {
        float shifted = 0.0;

        shifted = max(shifted, SampleMask(maskUv - offsetUv));
        shifted = max(shifted, SampleMask(maskUv - offsetUv * 0.66));
        shifted = max(shifted, SampleMask(maskUv - offsetUv * 0.33));

        return saturate(shifted - original);
    }

    float BuildInsideRim(float2 maskUv, float2 offsetUv, float original)
    {
        float shifted = 1.0;

        shifted = min(shifted, SampleMask(maskUv + offsetUv));
        shifted = min(shifted, SampleMask(maskUv + offsetUv * 0.66));
        shifted = min(shifted, SampleMask(maskUv + offsetUv * 0.33));

        return saturate(original - shifted);
    }

    float BuildOffsetRim(float2 maskUv)
    {
        float2 offsetUv = _OffsetPixels * _MaskTexelSize.xy;
        float original = SampleMask(maskUv);
        float inside = BuildInsideRim(maskUv, offsetUv, original);
        float outside = BuildOutsideRim(maskUv, offsetUv, original);

        if (_RimPlacement == 1)
            return outside;

        if (_RimPlacement == 2)
            return max(inside, outside);

        return inside;
    }

    float4 FragmentMask(MeshVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return 1.0;
    }

    float4 FragmentComposite(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 cameraUv = CameraUv(input.texcoord);
        float2 maskUv = MaskUv(input.texcoord);
        float mask = SampleMask(maskUv);
        float rim = BuildOffsetRim(maskUv);
        float3 rimColor = _Color.rgb * _Color.a * _Intensity * rim;

        if (_DebugMode > 1.5)
            return float4(rimColor, 1.0);

        if (_DebugMode > 0.5)
            return float4(mask.xxx, 1.0);

        float4 cameraColor = SAMPLE_TEXTURE2D_X(_CameraColorTexture, s_linear_clamp_sampler, cameraUv);
        cameraColor.rgb += rimColor;
        return cameraColor;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Mask"
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragmentMask
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentComposite
            ENDHLSL
        }
    }

    Fallback Off
}
