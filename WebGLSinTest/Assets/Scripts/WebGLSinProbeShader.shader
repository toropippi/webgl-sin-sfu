Shader "Hidden/WebGLSinProbe"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // highp 相当
            #define REAL float

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // 入力 9 個と RT サイズ
            CBUFFER_START(UnityPerMaterial)
                REAL _Inputs[9];
                int  _RTWidth;     // 32
                int  _RTHeight;    // 9
            CBUFFER_END

            // 32x9 の各ピクセルに 1bit を出力（R=G=B=0 or 1）
            float4 frag(v2f i) : SV_Target
            {
                // uv から整数ピクセル座標へ（端はクランプ）
                int x = clamp((int)floor(i.uv.x * _RTWidth ),  0, _RTWidth  - 1);
                int y = clamp((int)floor(i.uv.y * _RTHeight),  0, _RTHeight - 1);

                // y 行の入力値で sin、ビットを抽出
                REAL  xin  = _Inputs[y];
                REAL  sval = sin(xin);
                uint  ub   = asuint(sval);         // float → bit 列
                uint  bit  = (ub >> x) & 1u;       // 下位から x 番目の 1bit

                float v = (bit != 0u) ? 1.0 : 0.0; // UNorm (0 or 1) に格納
                return float4(v, v, v, 1.0);
            }
            ENDCG
        }
    }
}
