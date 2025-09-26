// Assets/_MyAssets/2_Shaders/DeterministicOverlayHUD.shader
Shader "Unlit/DeterministicOverlayHUD"
{
    Properties { }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = float4(v.positionOS.xy * 2.0 - 1.0, 0.0, 1.0);
                o.uv = v.uv;
                return o;
            }

            // Blit source
            TEXTURE2D(_BlitTex);
            SAMPLER(sampler_LinearClamp);

            // From C#
            int   _FrameIndex;
            float _TimeNow;
            float2 _TargetSize;
            float  _HudScale; 
            float2 _HudFlip;

            // ----- 5x7 digit rows (decimal masks, 0=top row .. 6=bottom) -----
            uint GetDigitRowMask(int d, int y)
            {
                y = clamp(y,0,6);
                switch (d)
                {
                    case 0: switch(y){case 0:return 14u; case 1:return 17u; case 2:return 17u; case 3:return 17u; case 4:return 17u; case 5:return 17u; default:return 14u;}
                    case 1: switch(y){case 0:return 4u;  case 1:return 12u; case 2:return 4u;  case 3:return 4u;  case 4:return 4u;  case 5:return 4u;  default:return 14u;}
                    case 2: switch(y){case 0:return 14u; case 1:return 17u; case 2:return 1u;  case 3:return 6u;  case 4:return 12u; case 5:return 16u; default:return 31u;}
                    case 3: switch(y){case 0:return 30u; case 1:return 1u;  case 2:return 6u;  case 3:return 1u;  case 4:return 1u;  case 5:return 17u; default:return 14u;}
                    case 4: switch(y){case 0:return 2u;  case 1:return 6u;  case 2:return 10u; case 3:return 18u; case 4:return 31u; case 5:return 2u;  default:return 2u; }
                    case 5: switch(y){case 0:return 31u; case 1:return 16u; case 2:return 30u; case 3:return 1u;  case 4:return 1u;  case 5:return 17u; default:return 14u;}
                    case 6: switch(y){case 0:return 6u;  case 1:return 8u;  case 2:return 16u; case 3:return 30u; case 4:return 17u; case 5:return 17u; default:return 14u;}
                    case 7: switch(y){case 0:return 31u; case 1:return 1u;  case 2:return 2u;  case 3:return 4u;  case 4:return 8u;  case 5:return 8u;  default:return 8u; }
                    case 8: switch(y){case 0:return 14u; case 1:return 17u; case 2:return 17u; case 3:return 14u; case 4:return 17u; case 5:return 17u; default:return 14u;}
                    default:/*9*/ switch(y){case 0:return 14u; case 1:return 17u; case 2:return 17u; case 3:return 15u; case 4:return 1u;  case 5:return 2u;  default:return 12u;}
                }
            }
            bool GlyphDot(int d, int x, int yTop)
            {
                uint row = GetDigitRowMask(d, yTop);
                return ((row >> (4 - x)) & 1u) != 0u;
            }

            float DrawDigit5x7(float2 px, int d, float2 bl, float dot, float gap)
            {
                float2 rel = px - bl;
                if (rel.x < 0 || rel.y < 0) return 0.0;

                float stepX = dot + gap;
                float stepY = dot + gap;
                float W = stepX * 5.0 - gap;
                float H = stepY * 7.0 - gap;
                if (rel.x > W || rel.y > H) return 0.0;

                int gx = (int)floor(rel.x / stepX);
                int gy = (int)floor(rel.y / stepY); // 0 at bottom
                if (gx < 0 || gx > 4 || gy < 0 || gy > 6) return 0.0;

                float2 dBL = float2(gx * stepX, gy * stepY);
                float2 dTR = dBL + float2(dot, dot);
                float inside = step(0.0, -max(max(dBL.x - rel.x, rel.x - dTR.x), max(dBL.y - rel.y, rel.y - dTR.y)));

                int yTop = 6 - gy;
                return GlyphDot(d, gx, yTop) ? inside : 0.0;
            }

            float DrawNumber5x7(float2 px, int value, float2 originBL, float dot, float gap, int digits, float digitGap)
            {
                float a = 0.0;
                int v = abs(value);
                float digitW = dot * 5.0 + gap * 4.0;

                [unroll] for (int i = 0; i < 12; i++)
                {
                    if (i >= digits) break;
                    int d = v % 10; v /= 10;
                    float2 bl = originBL + float2((digits - 1 - i) * (digitW + digitGap), 0);
                    a = max(a, DrawDigit5x7(px, d, bl, dot, gap));
                }
                return a;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 target = (_TargetSize.x > 0.0) ? _TargetSize : float2(1920.0,1080.0);

                float4 col = SAMPLE_TEXTURE2D(_BlitTex, sampler_LinearClamp, i.uv);

                float2 uvHUD = float2(i.uv.x, 1.0 - i.uv.y);

                float2 px = uvHUD * target;

                // ===== layout & sizing =====
                float s = (_HudScale > 0.0) ? _HudScale : 1.8;
                float DOT  = 3.0 * s;
                float GAP  = 1.5 * s;
                float DGAP = 6.0 * s;
                float GGAP = 12.0 * s;

                float digitW = DOT*5.0 + GAP*4.0;
                float digitH = DOT*7.0 + GAP*6.0;
                float marginX = 16.0 * s;
                float marginY = 16.0 * s;
                float2 originBL = float2(marginX, target.y - marginY - digitH);

                float totalW = digitW*6.0 + GGAP + digitW*7.0 + DGAP*(6.0+7.0-2.0);
                float2 bgBL = originBL + float2(-8.0*s, -8.0*s);
                float2 bgTR = bgBL + float2(totalW + 16.0*s, digitH + 16.0*s);
                float inBG  = step(0.0, -max(max(bgBL.x - px.x, px.x - bgTR.x), max(bgBL.y - px.y, px.y - bgTR.y)));
                col = lerp(col, float4(0,0,0,1), 0.75 * inBG);

                // ===== draw numbers =====
                float a0 = DrawNumber5x7(px, _FrameIndex, originBL, DOT, GAP, 6, DGAP);

                int tMs = (int)round(_TimeNow * 1000.0);
                float2 originBL2 = originBL + float2(digitW*6.0 + GGAP + DGAP*(6.0-1.0), 0.0);
                float a1 = DrawNumber5x7(px, tMs, originBL2, DOT, GAP, 7, DGAP);

                float a = saturate(max(a0, a1));
                col = lerp(col, float4(1,1,1,1), a); // white dots
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
