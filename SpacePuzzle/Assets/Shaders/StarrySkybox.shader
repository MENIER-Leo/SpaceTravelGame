Shader "StarrySkybox"
{
    Properties
    {
        _TopColor ("Couleur haut", Color) = (0.01, 0.01, 0.04, 1)
        _HorizonColor ("Couleur horizon", Color) = (0.05, 0.03, 0.12, 1)
        _BottomColor ("Couleur bas", Color) = (0.01, 0.01, 0.03, 1)
        _StarScale ("Échelle des étoiles", Float) = 60
        _StarDensity ("Densité (0-1)", Range(0, 1)) = 0.25
        _StarSize ("Taille des étoiles", Range(0.05, 0.4)) = 0.1
        _Brightness ("Luminosité", Float) = 2.5
        _Twinkle ("Vitesse de scintillement", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor;
            float _StarScale, _StarDensity, _StarSize, _Brightness, _Twinkle;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float3 starLayer(float3 dir, float scale, float sizeMul, float seed)
            {
                float3 p = dir * scale;
                float3 id = floor(p) + seed;
                float3 f = frac(p);

                float rnd = hash31(id);
                if (rnd > _StarDensity) return 0;

                float3 pos = 0.3 + 0.4 * hash33(id);
                float d = length(f - pos);
                float s = smoothstep(_StarSize * sizeMul, 0.0, d);
                s *= s;

                float tw = 0.75 + 0.25 * sin(_Time.y * _Twinkle * (1 + rnd * 5) + rnd * 100);
                float bright = 0.4 + 0.6 * (rnd / _StarDensity);

                // Teinte : bleu -> blanc -> orange
                float t = hash31(id + 7.7);
                float3 tint = lerp(float3(0.6, 0.75, 1.0), float3(1.0, 0.85, 0.65), t);
                tint = lerp(tint, 1, 0.5);

                return tint * s * tw * bright;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                // Dégradé de fond
                float h = dir.y;
                float3 col = h > 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, saturate(h))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-h));

                // 3 couches d'étoiles (lointaines -> proches)
                float3 stars = 0;
                stars += starLayer(dir, _StarScale,        1.0, 0.0);
                stars += starLayer(dir, _StarScale * 0.5,  1.5, 13.0);
                stars += starLayer(dir, _StarScale * 1.8,  0.7, 29.0);

                col += stars * _Brightness;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}