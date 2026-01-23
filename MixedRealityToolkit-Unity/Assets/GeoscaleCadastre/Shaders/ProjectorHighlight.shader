Shader "GeoscaleCadastre/ProjectorHighlight"
{
    Properties
    {
        _Color ("Highlight Color", Color) = (0, 0.9, 0.75, 0.5)
        _ShadowTex ("Cookie", 2D) = "white" {}
    }

    Subshader
    {
        Tags { "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 uvShadow : TEXCOORD0;
                float4 uvClip : TEXCOORD1;
                float4 pos : SV_POSITION;
            };

            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uvShadow = mul(unity_Projector, v.vertex);
                o.uvClip = mul(unity_ProjectorClip, v.vertex);
                return o;
            }

            sampler2D _ShadowTex;
            fixed4 _Color;

            fixed4 frag(v2f i) : SV_Target
            {
                // Calculer les UV normalisées
                float2 uv = i.uvShadow.xy / i.uvShadow.w;

                // Clipper les pixels en dehors de la zone [0,1]
                // Si UV < 0 ou UV > 1, on est en dehors de la texture
                float inBoundsX = step(0, uv.x) * step(uv.x, 1);
                float inBoundsY = step(0, uv.y) * step(uv.y, 1);
                float inBounds = inBoundsX * inBoundsY;

                // Clipper aussi les pixels derrière le projector
                float behindProjector = step(0, i.uvShadow.w);

                // Clipper avec le frustum du projector (near/far)
                float clipVal = i.uvClip.x / i.uvClip.w;
                float inFrustum = step(0, clipVal) * step(clipVal, 1);

                // Sampler la texture
                fixed4 texS = tex2D(_ShadowTex, uv);

                // Appliquer la couleur seulement où la texture n'est pas transparente
                fixed4 col = _Color;
                col.a *= texS.a;

                // Appliquer tous les clips
                col.a *= inBounds * behindProjector * inFrustum;

                // Rejeter les pixels complètement transparents
                clip(col.a - 0.001);

                return col;
            }
            ENDCG
        }
    }
}
