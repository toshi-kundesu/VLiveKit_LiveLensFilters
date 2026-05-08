Shader "Hidden/Kino/PostProcess/Streak"
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

    // ★追加：CopyPass が SetGlobalTexture で配ったDepth（RenderTexture）
    // CopyPass 側の globalDepthTextureName と一致させること（例："_CopiedDepthTex"）
    TEXTURE2D(_CopiedColorTex);
    TEXTURE2D(_CopiedNormalTex);
    TEXTURE2D(_CopiedRoughnessTex);
    TEXTURE2D(_CopiedDepthTex);
    TEXTURE2D(_CopiedMotionVectorsTex);
    TEXTURE2D(_SeeThroughFinalTex);


    float4 _InputTexture_TexelSize;

    float _Threshold;
    float _Stretch;
    float _Intensity;
    float3 _Color;
    float _Intensity_d;
    float _Intensity_c;
    float _Intensity_n;
    float _Intensity_r;
    float _Intensity_m;

    // Prefilter: Shrink horizontally and apply threshold.
    float4 FragmentPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    // 輝度抽出元をマスクして、その後輝度抽出を行う

        float3 s = SAMPLE_TEXTURE2D(_SeeThroughFinalTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 mask = float3(s.r, s.r, s.r);
        uint2 ss = input.texcoord * _ScreenSize.xy - float2(0, 0.5);
        float3 c0 = LOAD_TEXTURE2D_X(_SourceTexture, ss).rgb * mask;
        float3 c1 = LOAD_TEXTURE2D_X(_SourceTexture, ss + uint2(0, 1)).rgb * mask;
        float3 c = (c0 + c1) / 2;

        float br = max(c.r, max(c.g, c.b));
        c *= max(0, br - _Threshold) / max(br, 1e-5);

        // c = s;

        return float4(c, 1);
    }

    // Downsampler
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

    // Upsampler
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

    // Final composition
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

    // ★追加：グローバルで受け取った Depth テクスチャをそのまま描画（デバッグ表示）
    float4 FragmentShowDepth(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 d = SAMPLE_TEXTURE2D(_CopiedDepthTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 c = SAMPLE_TEXTURE2D(_CopiedColorTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 n = SAMPLE_TEXTURE2D(_CopiedNormalTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 r = SAMPLE_TEXTURE2D(_CopiedRoughnessTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 m = SAMPLE_TEXTURE2D(_CopiedMotionVectorsTex, s_linear_clamp_sampler, input.texcoord).rgb;
        float3 s = SAMPLE_TEXTURE2D(_SeeThroughFinalTex, s_linear_clamp_sampler, input.texcoord).rgb;
        // 値域が 0-1 じゃない場合、真っ白/真っ黒になりがちだけど
        // “最小限”のため、とりあえず saturate のみにしてる
        // d = saturate(d);
        // c = saturate(c);
        // n = saturate(n);
        // r = saturate(r);
        // m = saturate(m);
        // s = saturate(s);
        float3 result = _Intensity_d * d + _Intensity_c * c + _Intensity_n * n + _Intensity_r * r + _Intensity_m * m;
        result = float3(s.r, s.g, 1);


        return float4(d, 1);
    }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentPrefilter
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentDownsample
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentUpsample
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentComposition
            ENDHLSL
        }

        // ★追加：pass 4 = Depth表示
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentShowDepth
            ENDHLSL
        }
    }

    Fallback Off
}
