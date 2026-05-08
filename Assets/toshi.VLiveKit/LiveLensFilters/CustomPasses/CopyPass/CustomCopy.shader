Shader "Hidden/FullScreen/CustomCopy"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 _Scale;

    // ★追加：Depth出力モード
    // 0: Raw (LoadCameraDepthそのまま)
    // 1: Linear01 (near..farを0..1へ)
    // 2: Eye (posInput.linearDepth)
    int _DepthOutputMode;

    // ★変更：rawDepthも渡す
    float4 SampleBuffer(PositionInputs posInput, float rawDepth);

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float rawDepth = LoadCameraDepth(varyings.positionCS.xy);

        PositionInputs posInput = GetPositionInput(
            varyings.positionCS.xy,
            _ScreenSize.zw,
            rawDepth,
            UNITY_MATRIX_I_VP,
            UNITY_MATRIX_V
        );

        return SampleBuffer(posInput, rawDepth);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Normal"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

            float4 SampleBuffer(PositionInputs posInput, float rawDepth)
            {
                NormalData normalData;
                DecodeFromNormalBuffer(posInput.positionSS.xy, normalData);
                return float4(normalData.normalWS, 1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Roughness"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

            float4 SampleBuffer(PositionInputs posInput, float rawDepth)
            {
                NormalData normalData;
                DecodeFromNormalBuffer(posInput.positionSS.xy, normalData);
                return float4(normalData.perceptualRoughness, 0, 0, 1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Depth"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass

            float4 SampleBuffer(PositionInputs posInput, float rawDepth)
            {
                // Raw: デバイス深度（LoadCameraDepthの生値）
                float raw = rawDepth;

                // Eye: カメラからの線形距離（URPのEye相当）
                float eye = posInput.linearDepth;

                // Linear01: near..far を 0..1 に正規化
                // UnityのProjectionParams: y=near, z=far
                float nearP = _ProjectionParams.y;
                float farP  = _ProjectionParams.z;
                float lin01 = saturate((eye - nearP) / max(1e-6, (farP - nearP)));

                float v = ( _DepthOutputMode == 0 ) ? raw :
                          ( _DepthOutputMode == 1 ) ? lin01 :
                                                     eye;

                return float4(v, v, v, 1);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
