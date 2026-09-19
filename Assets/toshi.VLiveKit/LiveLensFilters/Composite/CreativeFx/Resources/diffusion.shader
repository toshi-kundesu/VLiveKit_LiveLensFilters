Shader "Hidden/toshi/LensFilters/Diffusion"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
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

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_InputTexture);
    TEXTURE2D_X(_BlurTexture);
    TEXTURE2D_X(_SourceTexture);

    float _Stretch;
    float _Threshold;
    float _BlurRadius;
    float _MinimumBlur;
    float _Intensity;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _BloomIntensity;
    float4 _BloomColor;
    int _SourceMode;
    int _BlendMode;
    int _UseTint;
    float4 _Tint;

    static const float GaussianCenterWeight = 0.19648255;
    static const float GaussianNearWeight = 0.29690696;
    static const float GaussianMiddleWeight = 0.09447040;
    static const float GaussianFarWeight = 0.01038136;
    static const float GaussianNearOffset = 1.41176471;
    static const float GaussianMiddleOffset = 3.29411765;
    static const float GaussianFarOffset = 5.17647059;

    float4 SampleInput(float2 uv)
    {
        float2 sampleUV = ClampAndScaleUVForBilinearPostProcessTexture(uv);
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, sampleUV);
    }

    float3 SampleBlur(float2 uv)
    {
        float2 sampleUV = ClampAndScaleUVForBilinearPostProcessTexture(uv);
        return SAMPLE_TEXTURE2D_X(_BlurTexture, s_linear_clamp_sampler, sampleUV).rgb;
    }

    float4 SampleSource(float2 uv)
    {
        float2 sampleUV = ClampAndScaleUVForBilinearPostProcessTexture(uv);
        return SAMPLE_TEXTURE2D_X(_SourceTexture, s_linear_clamp_sampler, sampleUV);
    }

    float Luma(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    float3 Highlight(float3 color)
    {
        float brightness = max(color.r, max(color.g, color.b));
        float knee = max(_Threshold * 0.2, 1.0e-4);
        float soft = saturate((brightness - _Threshold + knee) / (2.0 * knee));
        soft = soft * soft * knee;
        float contribution = max(brightness - _Threshold, soft) / max(brightness, 1.0e-4);
        return color * saturate(contribution);
    }

    float3 SourceSample(float2 uv)
    {
        float3 color = SampleInput(uv).rgb;
        return _SourceMode == 0 ? color : Highlight(color);
    }

    float3 BlurHorizontal(float2 uv)
    {
        float pixelRadius = max(_BlurRadius, 0.0) * lerp(1.0, 1.65, saturate(_Stretch));
        float2 stepUV = float2(_PostProcessScreenSize.z * max(pixelRadius, 1.0), 0.0);

        float3 center = SourceSample(uv);
        float3 sum = center * GaussianCenterWeight;
        sum += SourceSample(uv + stepUV * GaussianNearOffset) * GaussianNearWeight;
        sum += SourceSample(uv - stepUV * GaussianNearOffset) * GaussianNearWeight;
        sum += SourceSample(uv + stepUV * GaussianMiddleOffset) * GaussianMiddleWeight;
        sum += SourceSample(uv - stepUV * GaussianMiddleOffset) * GaussianMiddleWeight;
        sum += SourceSample(uv + stepUV * GaussianFarOffset) * GaussianFarWeight;
        sum += SourceSample(uv - stepUV * GaussianFarOffset) * GaussianFarWeight;
        // Keep paired bilinear taps at neighboring texels for subpixel radii.
        return lerp(center, sum, saturate(max(_MinimumBlur, pixelRadius * pixelRadius)));
    }

    float3 BlurTexture(float2 uv, float2 stepUV, float blurBlend)
    {
        float3 center = SampleBlur(uv);
        float3 sum = center * GaussianCenterWeight;
        sum += SampleBlur(uv + stepUV * GaussianNearOffset) * GaussianNearWeight;
        sum += SampleBlur(uv - stepUV * GaussianNearOffset) * GaussianNearWeight;
        sum += SampleBlur(uv + stepUV * GaussianMiddleOffset) * GaussianMiddleWeight;
        sum += SampleBlur(uv - stepUV * GaussianMiddleOffset) * GaussianMiddleWeight;
        sum += SampleBlur(uv + stepUV * GaussianFarOffset) * GaussianFarWeight;
        sum += SampleBlur(uv - stepUV * GaussianFarOffset) * GaussianFarWeight;
        return lerp(center, sum, blurBlend);
    }

    float3 BlurVertical(float2 uv)
    {
        float pixelRadius = max(_BlurRadius, 0.0) * lerp(1.0, 0.85, saturate(_Stretch));
        float2 stepUV = float2(0.0, _PostProcessScreenSize.w * max(pixelRadius, 1.0));
        return BlurTexture(uv, stepUV, saturate(max(_MinimumBlur, pixelRadius * pixelRadius)));
    }

    float3 BlurHorizontalTexture(float2 uv)
    {
        float pixelRadius = max(_BlurRadius, 0.0) * lerp(1.0, 1.65, saturate(_Stretch));
        float2 stepUV = float2(_PostProcessScreenSize.z * max(pixelRadius, 1.0), 0.0);
        return BlurTexture(uv, stepUV, saturate(max(_MinimumBlur, pixelRadius * pixelRadius)));
    }

    float3 ApplyGrade(float3 color)
    {
        color *= _Exposure;
        float luminance = Luma(color);
        color = lerp(luminance.xxx, color, _Saturation);
        color = (color - 0.5) * _Contrast + 0.5;
        return max(color, 0.0);
    }

    float3 ScreenBlend(float3 source, float3 glow)
    {
        return 1.0 - (1.0 - saturate(source)) * (1.0 - saturate(glow));
    }

    float4 FragmentHorizontal(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(BlurHorizontal(input.texcoord), 1.0);
    }

    float4 FragmentVertical(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(BlurVertical(input.texcoord), 1.0);
    }

    float4 FragmentHorizontalBlur(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(BlurHorizontalTexture(input.texcoord), 1.0);
    }

    float4 FragmentComposite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float4 source = SampleSource(input.texcoord);
        float3 glow = BlurVertical(input.texcoord) * _BloomColor.rgb * _BloomIntensity;
        if (_UseTint != 0)
            glow *= _Tint.rgb;

        glow = ApplyGrade(glow);
        float3 blended = _BlendMode == 0 ? max(source.rgb, glow) : ScreenBlend(source.rgb, glow);
        return float4(lerp(source.rgb, blended, saturate(_Intensity)), source.a);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentHorizontal
            ENDHLSL
        }

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentVertical
            ENDHLSL
        }

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentHorizontalBlur
            ENDHLSL
        }

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentComposite
            ENDHLSL
        }
    }
    Fallback Off
}
