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

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
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

                if (_CRTEffects.z > 0.0)
                {
                    float2 offset = p * dot(p, p) * 0.5 * _CRTEffects.z / _CRTScreen.xy;
                    color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                        clamp(sampleUV + offset, halfTexel, 1.0 - halfTexel)).r;
                    color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                        clamp(sampleUV - offset, halfTexel, 1.0 - halfTexel)).b;
                }

                float scanlineMask = pow(saturate(0.5 + 0.5 * cos(uv.y * _CRTScanlines.y * TWO_PI)), _CRTScanlines.z);
                color.rgb *= 1.0 - scanlineMask * _CRTScanlines.x;

                float2 grain = floor(uv * _CRTScreen.zw / _CRTNoise.z);
                float noise = Hash(grain + float2(_CRTNoise.y * 17.0, _CRTNoise.y * 29.0)) - 0.5;
                color.rgb = max(0.0, color.rgb + noise * _CRTNoise.x);

                float radius = length(p) * 0.70710678;
                float vignette = smoothstep(_CRTVignette.y,
                    _CRTVignette.y + max(_CRTVignette.z, 0.001), radius);
                color.rgb *= 1.0 - vignette * _CRTVignette.x;

                // Signed distance to the curved source rectangle; the radius uses the
                // shorter screen dimension, so corners stay round on wide/tall screens.
                float r = _CRTEffects.y;
                float2 q = abs((sampleUV - 0.5) * aspect) - (0.5 * aspect - r);
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float edge = max(fwidth(distance), 0.00001);
                float mask = 1.0 - smoothstep(-edge, edge, distance);
                if (_CRTEffects.x <= 0.0 && r <= 0.0) mask = 1.0;
                return half4(color.rgb * mask, color.a);
            }
            ENDHLSL
        }
    }
}
