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
    float4 _InputTexture_TexelSize;

    float _Stretch;
    float _Threshold;
    float _BlurRadius;
    float _Intensity;
    float4 _BloomWeights;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _BloomIntensity;
    float4 _BloomColor;
    int _SourceMode;
    int _BlendMode;
    int _UseTint;
    float4 _Tint;

    float4 SampleInput(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, saturate(uv));
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

    float3 DiffusionBlur(float2 uv)
    {
        float2 texel = _InputTexture_TexelSize.xy * max(_BlurRadius, 0.001);
        texel.x *= lerp(1.0, 1.65, saturate(_Stretch));
        texel.y *= lerp(1.0, 0.85, saturate(_Stretch));

        float4 weights = max(_BloomWeights, float4(0.04, 0.03, 0.02, 0.01));
        float3 sum = SourceSample(uv) * 0.2;
        float total = 0.2;

        sum += SourceSample(uv + texel * float2( 1.0,  0.0)) * weights.x;
        sum += SourceSample(uv + texel * float2(-1.0,  0.0)) * weights.x;
        sum += SourceSample(uv + texel * float2( 0.0,  1.0)) * weights.x;
        sum += SourceSample(uv + texel * float2( 0.0, -1.0)) * weights.x;
        total += weights.x * 4.0;

        sum += SourceSample(uv + texel * float2( 1.8,  1.8)) * weights.y;
        sum += SourceSample(uv + texel * float2(-1.8,  1.8)) * weights.y;
        sum += SourceSample(uv + texel * float2( 1.8, -1.8)) * weights.y;
        sum += SourceSample(uv + texel * float2(-1.8, -1.8)) * weights.y;
        total += weights.y * 4.0;

        sum += SourceSample(uv + texel * float2( 3.5,  0.0)) * weights.z;
        sum += SourceSample(uv + texel * float2(-3.5,  0.0)) * weights.z;
        sum += SourceSample(uv + texel * float2( 0.0,  3.5)) * weights.z;
        sum += SourceSample(uv + texel * float2( 0.0, -3.5)) * weights.z;
        total += weights.z * 4.0;

        sum += SourceSample(uv + texel * float2( 5.0,  2.5)) * weights.w;
        sum += SourceSample(uv + texel * float2(-5.0,  2.5)) * weights.w;
        sum += SourceSample(uv + texel * float2( 5.0, -2.5)) * weights.w;
        sum += SourceSample(uv + texel * float2(-5.0, -2.5)) * weights.w;
        total += weights.w * 4.0;

        return sum / max(total, 1.0e-4);
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

    float4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float4 source = SampleInput(input.texcoord);
        float3 glow = DiffusionBlur(input.texcoord) * _BloomColor.rgb * _BloomIntensity;
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
            #pragma fragment Fragment
            ENDHLSL
        }
    }
    Fallback Off
}
