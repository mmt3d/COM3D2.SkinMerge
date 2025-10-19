Shader "SkinMerge/BlendMultiply"
{
    Properties
    {
        _BaseTex ("Base Texture", 2D) = "white" {}
        _BlendTex ("Overlay Texture", 2D) = "white" {}
        _Alpha ("Blend Alpha", Range(0,1)) = 1.0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseTex;
            sampler2D _BlendTex;
            float _Alpha;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 baseCol = tex2D(_BaseTex, i.uv);
                fixed4 blendCol = tex2D(_BlendTex, i.uv);
                fixed4 outCol;
                float blendAlpha = blendCol.a * _Alpha;
                float outAlpha = blendAlpha + baseCol.a * (1 - blendAlpha);
                outCol.rgb = (blendCol.rgb * baseCol.rgb * blendAlpha + baseCol.rgb * (1 - blendAlpha)) / outAlpha;
                outCol.a = outAlpha;
                return outCol;
            }
            ENDCG
        }
    }
}