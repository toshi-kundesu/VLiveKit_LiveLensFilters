Shader "Hidden/Renderers/VisibleMask_WriteStencil"
{
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "WriteVisibleStencil"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite Off
            ZTest Always
            Cull Back
            ColorMask 0

            Stencil
            {
                Ref 2          // UserBit1
                ReadMask 0
                WriteMask 2
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(ws);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
