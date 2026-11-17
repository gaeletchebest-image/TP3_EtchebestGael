Shader "Hidden/HandShooter/CRT"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Moderate CRT"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CRTEffects;   // curvature, corner radius, RGB offset in display pixels
            float4 _CRTScanlines; // strength, count, thinness
            float4 _CRTNoise;     // strength, discrete frame, grain size
            float4 _CRTVignette;  // strength, start, softness
            float4 _CRTScreen;    // display width/height, internal width/height
            float4 _CRTDeath;     // transition mode, progress
            float4 _CRTDash;      // strength, horizontal vanishing point, radial travel

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float DashStreaks(float2 screen, float2 aspect)
            {
                float edge = smoothstep(0.59, 0.92, max(abs(screen.x), abs(screen.y)));
                if (edge <= 0.0) return 0.0;

                float2 fromFocus = (screen - float2(_CRTDash.y, 0.0)) * aspect / max(aspect.x, aspect.y);
                float sector = (atan2(fromFocus.y, fromFocus.x) / TWO_PI + 0.5) * 58.0;
                float ray = floor(sector);
                float seed = Hash(float2(ray, 17.0));
                float rayMask = 1.0 - smoothstep(0.026, 0.086, abs(frac(sector) - lerp(0.28, 0.72, seed)));
                float wave = frac(length(fromFocus) * (1.0 + seed * 0.65) - _CRTDash.z + Hash(float2(ray, 31.0)));
                float trail = smoothstep(0.0, 0.12, wave) * (1.0 - smoothstep(0.23 + seed * 0.24, 0.60, wave));
                return edge * rayMask * trail * smoothstep(0.15, 0.85, seed);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float phase = _CRTDeath.y;
                float shutdownMask = 1.0;
                float phosphor = 0.0;
                float dropout = 0.0;
                if (_CRTDeath.x > 1.5)
                {
                    if (phase >= 0.91) return half4(0, 0, 0, 1);
                    float collapseY = smoothstep(0.0, 0.60, phase);
                    float collapseX = smoothstep(0.60, 0.90, phase);
                    float2 beam = max(float2(1.0 - collapseX, 1.0 - collapseY), 2.0 / _CRTScreen.xy);
                    uv = (uv - 0.5) / beam + 0.5;
                    shutdownMask = all(abs(uv - 0.5) <= 0.5) ? 1.0 : 0.0;
                    phosphor = smoothstep(0.45, 0.62, phase) * (1.0 - collapseX);
                }
                else if (_CRTDeath.x > 0.5)
                {
                    dropout = smoothstep(0.0, 0.09, phase) * (1.0 - smoothstep(0.46, 1.0, phase));
                    float frame = floor(phase * 12.0);
                    float band = frac(uv.y * 3.0 - frame * 0.23);
                    float tears = step(0.70, band) - step(band, 0.15);
                    float jitter = Hash(float2(floor(uv.y * 36.0), frame)) - 0.5;
                    uv.x += (tears * 0.16 + jitter * 0.085) * dropout;
                    uv.y += sin(phase * TWO_PI) * 0.047 * dropout;
                }
                float2 p = uv * 2.0 - 1.0;
                float2 aspect = _CRTScreen.xy / max(min(_CRTScreen.x, _CRTScreen.y), 1.0);
                float2 radial = p * aspect / max(aspect.x, aspect.y);
                // Outward UV mapping fits the entire source into a curved boundary.
                // Never zoom in to hide the narrow black border: that would crop gameplay.
                float2 curved = p * (1.0 + _CRTEffects.x * dot(radial, radial));
                float2 sampleUV = curved * 0.5 + 0.5;
                float2 halfTexel = 0.5 / _CRTScreen.zw;
                float2 safeUV = clamp(sampleUV, halfTexel, 1.0 - halfTexel);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, safeUV);

                if (_CRTEffects.z > 0.0 || dropout > 0.0)
                {
                    float2 offset = p * dot(p, p) * 0.5 * _CRTEffects.z / _CRTScreen.xy;
                    offset.x += dropout * (5.0 / _CRTScreen.x + 0.007);
                    color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                        clamp(sampleUV + offset, halfTexel, 1.0 - halfTexel)).r;
                    color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                        clamp(sampleUV - offset, halfTexel, 1.0 - halfTexel)).b;
                }

                float scanlineMask = pow(saturate(0.5 + 0.5 * cos(uv.y * _CRTScanlines.y * TWO_PI)), _CRTScanlines.z);
                color.rgb *= 1.0 - scanlineMask * _CRTScanlines.x;

                float2 grain = floor(uv * _CRTScreen.zw / _CRTNoise.z);
                float noise = Hash(grain + float2(_CRTNoise.y * 17.0, _CRTNoise.y * 29.0)) - 0.5;
                color.rgb *= 1.0 - dropout * 0.67;
                color.rgb = max(0.0, color.rgb + noise * (_CRTNoise.x + dropout * 0.24));

                float radius = length(p) * 0.70710678;
                float vignette = smoothstep(_CRTVignette.y,
                    _CRTVignette.y + max(_CRTVignette.z, 0.001), radius);
                color.rgb *= 1.0 - vignette * _CRTVignette.x;

                if (_CRTDash.x > 0.0)
                {
                    float streak = DashStreaks(p, aspect);
                    color.rgb = lerp(color.rgb, half3(0.68, 0.92, 1.0), streak * _CRTDash.x * 0.46);
                }

                // Signed distance to the curved source rectangle; the radius uses the
                // shorter screen dimension, so corners stay round on wide/tall screens.
                float r = _CRTEffects.y;
                float2 q = abs((sampleUV - 0.5) * aspect) - (0.5 * aspect - r);
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float edge = max(fwidth(distance), 0.00001);
                float mask = 1.0 - smoothstep(-edge, edge, distance);
                if (_CRTEffects.x <= 0.0 && r <= 0.0) mask = 1.0;
                color.rgb = lerp(color.rgb * mask, half3(0.74, 1.0, 0.88), phosphor);
                return half4(color.rgb * shutdownMask, color.a);
            }
            ENDHLSL
        }
    }
}
