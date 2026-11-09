// Scales a low-res pixel-art render target up by any (fractional) factor while
// keeping it crisp: each texel is drawn as a solid block and only the one screen
// pixel straddling a texel edge is blended. Plain bilinear would blur the whole
// image; plain point filtering would give uneven texel widths (visible stripes).
// The texture must use bilinear filtering for the edge blend to work.
Shader "OopsItAte/UI/SharpPixelUpscale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Opaque"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                float2 texelPosition = input.uv * _MainTex_TexelSize.zw - 0.5;
                float2 texelBase = floor(texelPosition);
                float2 texelFraction = texelPosition - texelBase;

                // Texels covered by one screen pixel; the blend between two texels is
                // squeezed into that width so everything else samples a single texel.
                float2 screenPixelInTexels = max(fwidth(texelPosition), 1e-5);
                texelFraction = saturate((texelFraction - 0.5) / screenPixelInTexels + 0.5);

                float2 uv = (texelBase + texelFraction + 0.5) * _MainTex_TexelSize.xy;
                fixed4 color = tex2D(_MainTex, uv);
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }
}
