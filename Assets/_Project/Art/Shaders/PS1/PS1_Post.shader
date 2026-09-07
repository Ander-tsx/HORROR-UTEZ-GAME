// Full-screen pass: colour-depth reduction with ordered dithering, plus a hard vignette.
//
// Driven by URP's built-in FullScreenPassRendererFeature, so there is no RenderGraph code to
// maintain — the fragile part of a custom post effect in URP 17 is the C# render pass, and this
// avoids having one at all.
//
// What it reproduces: the PS1 framebuffer held 5 bits per channel (32 levels, not 256), and
// games dithered before writing to it so the banding broke up into a stipple instead of showing
// as flat bands. That stipple is a large part of why the era looks the way it does, and it is
// invisible unless the render resolution is already low.
Shader "UtezHorror/PS1 Post"
{
    Properties
    {
        _ColorLevels("Colour Levels Per Channel", Range(2, 64)) = 32
        _DitherStrength("Dither Strength", Range(0, 2)) = 1
        _VignetteStrength("Vignette Strength", Range(0, 1)) = 0.45
        _VignettePower("Vignette Falloff", Range(1, 6)) = 2.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PS1Post"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment PostFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float _ColorLevels;
            float _DitherStrength;
            float _VignetteStrength;
            float _VignettePower;

            /// 4x4 Bayer matrix, normalised to -0.5..0.5. Written out rather than sampled from a
            /// texture so the effect has no asset dependency that can go missing.
            float Bayer4x4(uint2 pixel)
            {
                const float table[16] =
                {
                     0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                     3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                    15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
                };

                uint index = (pixel.y % 4) * 4 + (pixel.x % 4);
                return table[index] - 0.5;
            }

            half4 PostFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // Dither in the *render* resolution's pixel grid, not the window's, or the
                // pattern is upscaled into visible 3x3 blocks by the nearest-neighbour filter.
                uint2 pixel = (uint2)(uv * _ScreenSize.xy);

                float levels = max(_ColorLevels, 2.0);
                float step = 1.0 / (levels - 1.0);

                // Quantise in DISPLAY space, not linear.
                //
                // The project renders in linear colour, and the buffer reaching this pass is
                // still linear. A PS1's 5 bits per channel were 5 bits of what you actually saw,
                // which is gamma-encoded. Quantising the linear values instead puts the 32 steps
                // almost entirely in the highlights: the first step above black lands around 20%
                // display brightness, so every dark part of the game — which is most of it —
                // collapses onto black or jumps to a bright, saturated speck. That is what turned
                // the foggy wood into green and blue confetti.
                half3 display = LinearToSRGB(color);

                display += Bayer4x4(pixel) * step * _DitherStrength;
                display = floor(saturate(display) * (levels - 1.0) + 0.5) * step;

                color = SRGBToLinear(display);

                // Corners into darkness: the last item on the brief, and it doubles as a cheap
                // way to keep the player's eye on the torch beam.
                float2 centred = uv * 2.0 - 1.0;
                float vignette = 1.0 - _VignetteStrength * pow(saturate(length(centred) * 0.72), _VignettePower);
                color *= vignette;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
