Shader "Hidden/Renderers/VisibleMask_Overlay"
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
            Name "OverlayVisibleMask"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask 0
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            float _MaskAlpha;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(v.vertexID);
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
