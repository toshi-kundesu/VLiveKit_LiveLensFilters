Shader "Hidden/toshi/LensFilters/GenshinColorGrading"
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

    float _Threshold;
    float _Stretch;
    float _Intensity;
    float4 _Color;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _ToneMap;

    float4 SampleInput(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, saturate(uv));
    }

    float Luma(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    float3 ApplyGrade(float3 color)
    {
        color *= _Exposure;

        float luminance = Luma(color);
        color = lerp(luminance.xxx, color, _Saturation);
        color = (color - 0.5) * _Contrast + 0.5;
        color = max(color, 0.0);

        if (_ToneMap > 0.5)
            color = color / (1.0 + color);

        float tintAmount = saturate(_Stretch) * saturate(_Threshold) * 0.08;
        color = lerp(color, color * _Color.rgb, tintAmount);
        return color;
    }

    float4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float4 source = SampleInput(input.texcoord);
        float3 graded = ApplyGrade(source.rgb);
        return float4(lerp(source.rgb, graded, saturate(_Intensity)), source.a);
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
