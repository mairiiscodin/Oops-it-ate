Shader "OopsItAte/UI/IrisTransition"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Progress ("Open Progress", Range(0, 1)) = 1
        _OpenRadius ("Circle Open Radius", Float) = 1
        _Softness ("Edge Softness", Range(0.001, 0.1)) = 0.015
        _Silhouette ("Silhouette", 2D) = "white" {}
        _SilhouetteUvRect ("Silhouette UV Rect", Vector) = (0, 0, 1, 1)
        _SilhouetteAspect ("Silhouette Aspect", Float) = 1
        _SilhouetteOpenScale ("Silhouette Open Scale", Float) = 2.5
        _UseSilhouette ("Use Silhouette", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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

            fixed4 _Color;
            float _Progress;
            float _OpenRadius;
            float _Softness;
            sampler2D _Silhouette;
            float4 _SilhouetteUvRect;
            float _SilhouetteAspect;
            float _SilhouetteOpenScale;
            float _UseSilhouette;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                float2 offset = input.uv - 0.5;
                offset.x *= _ScreenParams.x / _ScreenParams.y;
                float distanceFromCenter = length(offset);
                float radius = lerp(-_Softness, _OpenRadius, _Progress);
                float circleOutside = smoothstep(
                    radius - _Softness,
                    radius + _Softness,
                    distanceFromCenter);

                float screenAspect = _ScreenParams.x / _ScreenParams.y;
                float2 silhouetteOffset = input.uv - 0.5;
                silhouetteOffset.x *= screenAspect / max(0.001, _SilhouetteAspect);
                float silhouetteScale = lerp(0.001, _SilhouetteOpenScale, _Progress);
                float2 silhouetteUv = silhouetteOffset / silhouetteScale + 0.5;
                float inBounds = step(0.0, silhouetteUv.x)
                    * step(silhouetteUv.x, 1.0)
                    * step(0.0, silhouetteUv.y)
                    * step(silhouetteUv.y, 1.0);
                float2 textureUv = _SilhouetteUvRect.xy
                    + saturate(silhouetteUv) * _SilhouetteUvRect.zw;
                float silhouetteOpening = tex2D(_Silhouette, textureUv).a * inBounds;
                float silhouetteOutside = 1.0 - silhouetteOpening;

                float outsideIris = lerp(
                    circleOutside,
                    silhouetteOutside,
                    saturate(_UseSilhouette));
                return fixed4(_Color.rgb, _Color.a * outsideIris);
            }
            ENDCG
        }
    }
}
