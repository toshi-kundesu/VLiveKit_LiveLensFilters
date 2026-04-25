Shader "Hidden/Kino/PostProcess/diffusion"
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
        float2 texcoord   : TEXCOORD0;
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

    TEXTURE2D_X(_SourceTexture);
    TEXTURE2D(_InputTexture);
    TEXTURE2D(_HighTexture);
    TEXTURE2D(_BloomTextureA);
    TEXTURE2D(_BloomTextureB);
    TEXTURE2D(_BloomTextureC);
    TEXTURE2D(_BloomTextureD);
    TEXTURE2D(_BlurTexture);

    float4 _InputTexture_TexelSize;

    float _Threshold;
    float _Stretch;
    float _Intensity;
    float3 _Color;

    float _BlurRadius;
    float4 _BloomWeights;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _BloomIntensity;
    float3 _BloomColor;
    // float _Intensity;
    float3 _Tint;


    const static int kernelSize = 9;
        const static float kernelOffsets[9] = {
            -4.0,
            -3.0,
            -2.0,
            -1.0,
            0.0,
            1.0,
            2.0,
            3.0,
            4.0,
        };
        const static float kernel[9] = {
            0.01621622,
            0.05405405,
            0.12162162,
            0.19459459,
            0.22702703,
            0.19459459,
            0.12162162,
            0.05405405,
            0.01621622
        };

        const static float Weights[9] = {0.5352615, 0.7035879, 0.8553453, 0.9616906, 1, 0.9616906, 0.8553453, 0.7035879, 0.5352615};
float3 Contrast(float3 In, float Contrast)
    {
        const float midpoint = pow(0.5, 2.2);
        return (In - midpoint) * Contrast + midpoint;
    }
    half4 Frag_Contrast(Varyings input) : SV_Target
    {
        half4 color = LOAD_TEXTURE2D_X(_SourceTexture, input.texcoord * _ScreenSize.xy);

        color.rgb = Contrast(color.rgb, _Contrast);
        // color.rgb = float3(_Contrast, 0, 0);
        
        return color;
    }
    half4 Frag_Blur1(Varyings input) : SV_Target
    {
        half4 color = 0;

        float totalWeight = 0;
        
        for(int i = -4; i <= 4; i++)
        {
            float weight = Weights[i + 4];
            totalWeight += weight;
            color += SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, input.texcoord + float4(i * _InputTexture_TexelSize.x * _BlurRadius, 0, 0, 0)) * weight;
        }

        color /= totalWeight;
        
        return color;
    }

    half4 Frag_Blur2(Varyings input) : SV_Target
    {
        half4 color = 0;

        float totalWeight = 0;
        
        for(int i = -4; i <= 4; i++)
        {
            float weight = Weights[i + 4];
            totalWeight += weight;
            color += SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, input.texcoord + float4(0, i * _InputTexture_TexelSize.y * _BlurRadius, 0, 0)) * weight;
        }

        color /= totalWeight;
        
        return color;
    }

    half4 Frag_Blend(Varyings input) : SV_Target
    {
        half4 color = LOAD_TEXTURE2D_X(_SourceTexture, input.texcoord * _ScreenSize.xy);
        half4 blur = SAMPLE_TEXTURE2D_X(_BlurTexture, s_linear_clamp_sampler, input.texcoord);

          color.rgb = 1.0 - (1.0 - color.rgb) * (1.0 - blur.rgb * _Intensity);
        // color.rgb = lerp(color.rgb, blur.rgb, _Intensity);
        
        return color;
    }
    half4 GaussianBlur(float2 uv, float2 direction)
        {
            float2 offset = _BlurRadius * _InputTexture_TexelSize * direction; 
            half4 color = 0.0;

            UNITY_UNROLL
            for (int i = 0; i < kernelSize; i++)
            {
                float2 sampleUV = uv + kernelOffsets[i] * offset;
                color += kernel[i] * SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, sampleUV);
            }

            return color;
        }

        half4 HorizontalBlur1x(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            return GaussianBlur(uv, float2(1.0, 0.0));
        }

        half4 HorizontalBlur2x(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            return GaussianBlur(uv, float2(2.0, 0.0));
        }

        half4 VerticalBlur1x(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            return GaussianBlur(uv, float2(0.0, 1.0));
        }

        half4 VerticalBlur2x(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            return GaussianBlur(uv, float2(0.0, 2.0));
        }


        half4 Upsample(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            half4 color = 0.0;
            half4 weights = _BloomWeights;

            color += SAMPLE_TEXTURE2D_X(_BloomTextureA, s_linear_clamp_sampler, uv) * weights.x;
            color += SAMPLE_TEXTURE2D_X(_BloomTextureB, s_linear_clamp_sampler, uv) * weights.y;
            color += SAMPLE_TEXTURE2D_X(_BloomTextureC, s_linear_clamp_sampler, uv) * weights.z;
            color += SAMPLE_TEXTURE2D_X(_BloomTextureD, s_linear_clamp_sampler, uv) * weights.w;

            return color;
        }
        half3 Tonemap(half3 color)
        {
            half3 c0 = (1.36 * color + 0.047) * color;
            half3 c1 = (0.93 * color + 0.56) * color + 0.14;
            return saturate(c0 / c1);
        }

        half4 ColorGrading(Varyings input) : SV_TARGET
        {
            float2 uv = input.texcoord;
            half4 baseMap = LOAD_TEXTURE2D_X(_SourceTexture, uv * _ScreenSize.xy);
            half3 color = baseMap.rgb;
            half alpha = baseMap.a;

// #if _BLOOM_COLOR || _BLOOM_BRIGHTNESS
            // Bloom
            half3 bloom = SAMPLE_TEXTURE2D_X(_BloomTextureA, s_linear_clamp_sampler, uv).rgb;
            bloom *= _BloomIntensity * _BloomColor.rgb;
            // bloom *= _BloomIntensity;
            color += bloom;
// #endif

            // Exposure
            color *= _Exposure;

// #if _TONEMAPPING
            // Tonemapping
            color = Tonemap(color);
// #endif

            // Contrast
            half3 colorLog = LinearToLogC(color);
            colorLog = lerp(ACEScc_MIDGRAY, colorLog, _Contrast);
            color = LogCToLinear(colorLog);

            // Saturation
            half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
            color = lerp(luma, color, _Saturation);

            return float4(color, 1);
        }

        // output bloomTextureA
        float4 FragmentBloomTextureA(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SAMPLE_TEXTURE2D(_BloomTextureA, s_linear_clamp_sampler, input.texcoord);
        }
        
    // 0: Prefilter: Shrink horizontally and apply threshold.
    float4 FragmentPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        uint2 ss = input.texcoord * _ScreenSize.xy;
        // float3 c0 = LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb;
        // float3 c1 = LOAD_TEXTURE2D_X(_SourceTexture, ss + uint2(0, 1)).rgb;
        // float3 c = (c0 + c1) / 2;
        float3 c = LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb;

        float br = max(c.r, max(c.g, c.b));
        c *= max(0, br - _Threshold) / max(br, 1e-5);

        return float4(c, 1);
    }

    // 1: Downsampler
    float4 FragmentDownsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        const float dx = _InputTexture_TexelSize.x;

        float u0 = uv.x - dx * 5;
        float u1 = uv.x - dx * 3;
        float u2 = uv.x - dx * 1;
        float u3 = uv.x + dx * 1;
        float u4 = uv.x + dx * 3;
        float u5 = uv.x + dx * 5;

        half3 c0 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u0, uv.y)).rgb;
        half3 c1 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u1, uv.y)).rgb;
        half3 c2 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u2, uv.y)).rgb;
        half3 c3 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u3, uv.y)).rgb;
        half3 c4 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u4, uv.y)).rgb;
        half3 c5 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u5, uv.y)).rgb;

        return half4((c0 + c1 * 2 + c2 * 3 + c3 * 3 + c4 * 2 + c5) / 12, 1);
    }

    // 2: Upsampler
    float4 FragmentUpsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        const float dx = _InputTexture_TexelSize.x * 1.5;

        float u0 = uv.x - dx;
        float u1 = uv.x;
        float u2 = uv.x + dx;

        float3 c0 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u0, uv.y)).rgb;
        float3 c1 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u1, uv.y)).rgb;
        float3 c2 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u2, uv.y)).rgb;
        float3 c3 = SAMPLE_TEXTURE2D(_HighTexture,  s_linear_clamp_sampler, uv).rgb;

        return float4(lerp(c3, c0 / 4 + c1 / 2 + c2 / 4, _Stretch), 1);
    }

    // 3: Final composition
    float4 FragmentComposition(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        uint2 positionSS = uv * _ScreenSize.xy;
        const float dx = _InputTexture_TexelSize.x * 1.5;

        float u0 = uv.x - dx;
        float u1 = uv.x;
        float u2 = uv.x + dx;

        float3 c0 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u0, uv.y)).rgb;
        float3 c1 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u1, uv.y)).rgb;
        float3 c2 = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(u2, uv.y)).rgb;
        float3 c3 = LOAD_TEXTURE2D_X(_SourceTexture, positionSS).rgb;
        float3 cf = (c0 / 4 + c1 / 2 + c2 / 4) * _Color * _Intensity * 5;

        return float4(cf + c3, 1);
    }
    // 4: InputTexture
    float4 FragmentInputTexture(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, input.texcoord);
    }
    // 5: SourceTexture
    float4 FragmentSourceTexture(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint2 ss = input.texcoord * _ScreenSize.xy;
        return LOAD_TEXTURE2D_X(_SourceTexture, ss);
    }

    // 6: SourceTexture + InputTexture
    float4 FragmentSourceTexturePlusInputTexture(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint2 ss = input.texcoord * _ScreenSize.xy;
        return LOAD_TEXTURE2D_X(_SourceTexture, ss) + SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, input.texcoord);
    }

    // 7: GaussianBlur
    float4 FragmentGaussianBlur(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return GaussianBlur(input.texcoord, float2(1.0, 0.0));
    }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass // 0: Prefilter
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentPrefilter
            ENDHLSL
        }
        Pass // 1: Downsampler
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentDownsample
            ENDHLSL
        }
        Pass // 2: Upsampler
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentUpsample
            ENDHLSL
        }
        Pass // 3: Final composition
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentComposition
            ENDHLSL
        }
        Pass // 4: InputTexture
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentInputTexture
            ENDHLSL
        }
        Pass // 5: SourceTexture
        {   
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentSourceTexture
            ENDHLSL
        }
        Pass // 6: SourceTexture + InputTexture
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentSourceTexturePlusInputTexture
            ENDHLSL
        }

        Pass // 7: HorizontalBlur1x
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment HorizontalBlur1x
            ENDHLSL
        }
        Pass // 8: HorizontalBlur2x
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment HorizontalBlur2x
            ENDHLSL
        }
        Pass // 9: VerticalBlur1x
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment VerticalBlur1x
            ENDHLSL
        }
        Pass // 10: VerticalBlur2x
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment VerticalBlur2x
            ENDHLSL
        }
        Pass // 11: Upsample
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Upsample
            ENDHLSL
        }
        Pass // 12: BloomTextureA
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentBloomTextureA
            ENDHLSL
        }
        Pass // 13: ColorGrading
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment ColorGrading
            ENDHLSL
        }
        Pass // 14: Contrast
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Frag_Contrast
            ENDHLSL
        }
        Pass // 15: Blur
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Frag_Blur1
            ENDHLSL
        }
        Pass // 16: Blur
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Frag_Blur2
            ENDHLSL
        }
        Pass // 17: Blend
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Frag_Blend
            ENDHLSL
        }
    }
    Fallback Off
}
