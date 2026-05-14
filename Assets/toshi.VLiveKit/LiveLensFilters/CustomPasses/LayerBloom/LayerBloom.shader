Shader "Hidden/toshi/LensFilters/CustomPass/LayerBloom"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_BloomTexture);

    float4 _MainTex_TexelSize;
    float4 _MainTexScaleBias;
    float _Threshold;
    float _SoftKnee;
    float _SourceBoost;
    float _BlurRadius;
    float _Intensity;
    int _ColorMode;
    float _MaxSourceBrightness;
    float _MaxBloomBrightness;
    float4 _Tint;

    static const int KernelSize = 9;
    static const float KernelOffsets[9] =
    {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    static const float KernelWeights[9] =
    {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    float3 ClampBrightness(float3 color, float maxBrightness)
    {
        float brightness = max(color.r, max(color.g, color.b));
        if (maxBrightness > 0.0 && brightness > maxBrightness)
            color *= maxBrightness / max(brightness, 1e-5);

        return color;
    }

    float3 PrefilterColor(float3 color)
    {
        color = max(color, 0.0);
        color *= _SourceBoost;
        color = ClampBrightness(color, _MaxSourceBrightness);

        float brightness = max(color.r, max(color.g, color.b));
        float knee = max(_Threshold * _SoftKnee, 1e-5);
        float soft = saturate((brightness - _Threshold + knee) / (2.0 * knee));
        soft = soft * soft * knee;
        float contribution = max(soft, brightness - _Threshold) / max(brightness, 1e-5);

        return ClampBrightness(color * saturate(contribution), _MaxSourceBrightness);
    }

    float Luma(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    float2 MainTexUv(float2 uv)
    {
        return uv * _MainTexScaleBias.xy + _MainTexScaleBias.zw;
    }

    float4 FragmentPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 color = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, MainTexUv(input.texcoord)).rgb;
        return float4(PrefilterColor(color), 1.0);
    }

    float4 GaussianBlur(Varyings input, float2 direction) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 offset = _MainTex_TexelSize.xy * _BlurRadius * direction;
        float3 color = 0.0;

        UNITY_UNROLL
        for (int i = 0; i < KernelSize; i++)
        {
            float2 uv = MainTexUv(input.texcoord + KernelOffsets[i] * offset);
            color += SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, uv).rgb * KernelWeights[i];
        }

        return float4(color, 1.0);
    }

    float4 FragmentHorizontalBlur(Varyings input) : SV_Target
    {
        return GaussianBlur(input, float2(1.0, 0.0));
    }

    float4 FragmentVerticalBlur(Varyings input) : SV_Target
    {
        return GaussianBlur(input, float2(0.0, 1.0));
    }

    float3 SampleBloom(float2 uv)
    {
        float3 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, s_linear_clamp_sampler, uv).rgb;

        if (_ColorMode == 1)
            bloom = Luma(bloom).xxx * _Tint.rgb;
        else if (_ColorMode == 2)
            bloom *= _Tint.rgb;

        bloom *= _Intensity;
        return ClampBrightness(bloom, _MaxBloomBrightness);
    }

    float4 FragmentComposite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float4 cameraColor = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, MainTexUv(input.texcoord));
        cameraColor.rgb += SampleBloom(input.texcoord);
        return cameraColor;
    }

    float4 FragmentBloomOnly(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(SampleBloom(input.texcoord), 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Prefilter"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragmentPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "Horizontal Blur"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragmentHorizontalBlur
            ENDHLSL
        }

        Pass
        {
            Name "Vertical Blur"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragmentVerticalBlur
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
            #pragma fragment FragmentComposite
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Only"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragmentBloomOnly
            ENDHLSL
        }
    }

    Fallback Off
}
