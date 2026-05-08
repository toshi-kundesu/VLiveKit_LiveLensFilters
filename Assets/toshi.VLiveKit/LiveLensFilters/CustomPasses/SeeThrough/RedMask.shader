Shader "Hidden/Renderers/RedMaskOverlay"
{
    Properties
    {
        _MaskAlpha ("Mask Alpha", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "RedMaskOverlay"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite Off
            ZTest Always      // ZTestはCustomPass側(RenderStateBlock)でLessEqualに上書き
            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float _MaskAlpha;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return float4(1, 0, 0, _MaskAlpha);
            }
            ENDHLSL
        }
    }
}
