Shader "Hidden/SeeThrough/OutsideStencilNotEqual"
{
    Properties
    {
        _Color ("Color", Color) = (1,0,0,0.35)
    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "Outside"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            // UserBit0 が「一致しない」ピクセルだけ描く
            // ※Ref=255 前提（あなたのステンシルシェーダが 255 書いてる前提）
            Stencil
            {
                Ref 255
                ReadMask 1
                WriteMask 0
                Comp NotEqual
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _Color;

            Varyings Vert(Attributes v)
            {
                Varyings o;

                // Fullscreen triangle: (-1,-1), (3,-1), (-1,3)
                float2 pos = float2((v.vertexID == 1) ? 3.0 : -1.0,
                                    (v.vertexID == 2) ? 3.0 : -1.0);

                o.positionCS = float4(pos, 0.0, 1.0);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
