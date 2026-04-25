Shader "Custom/Step" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Threshold", Range(0, 1)) = 0.5 // 閾値のプロパティを追加
    }
    SubShader {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass  {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Threshold; // 閾値の変数を追加
            
            struct v2f {
                float4 pos    : SV_POSITION; 
                float2 uv     : TEXCOORD0;
            };

            v2f vert(appdata_img v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag(const v2f i) : SV_Target {
                const float4 c = tex2D(_MainTex, i.uv);                
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float binary = step(_Threshold, gray); // 閾値を使用して二極化
                return float4(binary, binary, binary, c.a);
            }
            ENDCG
        }
    }
}