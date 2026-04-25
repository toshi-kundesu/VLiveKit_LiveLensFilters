Shader "Hidden/Kino/PostProcess/MyPostFx"
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

    float4 _InputTexture_TexelSize;

    float _Threshold;
    float _Stretch;
    float _Intensity;
    float3 _Color;

    // 0: Prefilter: Shrink horizontally and apply threshold.
    float4 FragmentPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        uint2 ss = input.texcoord * _ScreenSize.xy - float2(0, 0.5);
        float3 c0 = LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb;
        float3 c1 = LOAD_TEXTURE2D_X(_SourceTexture, ss + uint2(0, 1)).rgb;
        float3 c = (c0 + c1) / 2;

        float br = max(c.r, max(c.g, c.b));
        c *= max(0, br - _Threshold) / max(br, 1e-5);
        // c = float3(1, 0, 0);

        
        return float4(c, 1);
    }

    // 1: Downsampler
    float4 FragmentDownsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        half3 color = SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, input.texcoord).rgb;
        // step-1 2.5 テクセルずらすためのUV値を求める
        float scale = 5.0f;
        float offsetU = 1.5f / _ScreenSize.x * scale;
        float offsetV = 1.5f / _ScreenSize.y * scale;
        

        // 基準テクセルから右のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord + float2(offsetU, 0.0f))).rgb;

        // 基準テクセルから左のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord - float2(offsetU, 0.0f))).rgb;

        // 基準テクセルから上のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord + float2(0.0f, offsetV))).rgb;

        // 基準テクセルから下のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord - float2(0.0f, offsetV))).rgb;

        // 基準テクセルから右上のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord + float2(offsetU, offsetV))).rgb;

        // 基準テクセルから右下のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord - float2(offsetU, offsetV))).rgb;

        // 基準テクセルから左上のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord - float2(offsetU, offsetV))).rgb;

        // 基準テクセルから左下のテクセルカラーをサンプリング
        color += SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, float2(input.texcoord - float2(offsetU, offsetV))).rgb;
        
        // 基準テクセルと近傍8テクセルの平均なので9で除算する
        color /=  9.0f;
        // color *= float3(1, 1, 0);
        // 元々のソーステクスチャを足す   
        uint2 ss = input.texcoord * _ScreenSize.xy - float2(0, 0.5);
        color += LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb;
        // float3 c3 = LOAD_TEXTURE2D_X(_SourceTexture, positionSS).rgb;
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

        // return half4((c0 + c1 * 2 + c2 * 3 + c3 * 3 + c4 * 2 + c5) / 12, 1);
        
        return half4(color, 1);
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

    // 4: output input texture
    float4 FragmentInputTexture(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return SAMPLE_TEXTURE2D(_InputTexture, s_linear_clamp_sampler, input.texcoord);
    }

    // 5: output source texture
    float4 FragmentSourceTexture(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint2 ss = input.texcoord * _ScreenSize.xy - float2(0, 0.5);
        float3 c0 = LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb;
        // float3 c1 = LOAD_TEXTURE2D_X(_SourceTexture, ss + uint2(0, 1)).rgb;
        // float3 c = (c0 + c1) / 2;

        // float br = max(c.r, max(c.g, c.b));
        // c *= max(0, br - _Threshold) / max(br, 1e-5);
        // c = float3(1, 0, 0);

        
        return float4(c0, 1);
    }

    // 6: output red
    float4 FragmentRed(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(1, 0, 0, 1);
    }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass // 0
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentPrefilter
            ENDHLSL
        }
        Pass // 1
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentDownsample
            ENDHLSL
        }
        Pass // 2
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentUpsample
            ENDHLSL
        }
        Pass // 3
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentComposition
            ENDHLSL
        }
        Pass // 4   
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentInputTexture
            ENDHLSL
        }
        Pass // 5
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentSourceTexture
            ENDHLSL
        }
        Pass // 6
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentRed
            ENDHLSL
        }
    }
    Fallback Off
}
