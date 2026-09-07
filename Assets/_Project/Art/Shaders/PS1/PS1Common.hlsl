#ifndef UTEZ_PS1_COMMON_INCLUDED
#define UTEZ_PS1_COMMON_INCLUDED

// Shared building blocks for the PS1 look. See Docs/Plans/02-estilo-visual-ps1.md.
//
// Everything here reproduces a *limitation* of the hardware on purpose. Read the comments
// before "fixing" any of it: correct-looking maths here produces a modern-looking game.

// ---------------------------------------------------------------- vertex snapping

/// Quantises a clip-space position to a virtual screen grid.
///
/// The PS1's geometry unit had no floating point: vertex positions were computed as fixed-point
/// integers in screen space, so they visibly jump between whole pixels as the camera moves.
/// This is that jump.
///
/// The snap happens in NDC and the w is restored afterwards, so the perspective divide, the
/// depth value and clipping are all untouched — only the on-screen x/y move. Vertices behind
/// the camera (w <= 0) are left alone: snapping them mirrors the geometry across the screen.
float4 PS1_SnapVertex(float4 positionCS, float2 gridResolution)
{
    if (positionCS.w <= 0.0) return positionCS;

    // NDC spans -1..1 across the whole grid, hence the half.
    float2 grid = max(gridResolution, 2.0) * 0.5;
    float2 ndc = positionCS.xy / positionCS.w;
    ndc = floor(ndc * grid + 0.5) / grid;

    positionCS.xy = ndc * positionCS.w;
    return positionCS;
}

// ---------------------------------------------------------------- affine texture mapping

/// Packs a UV so it can be interpolated *without* perspective correction.
///
/// Hardware interpolates an attribute `a` as interp(a/w) / interp(1/w). Feeding it `uv * w`
/// and `w` separately and dividing the two in the fragment cancels the correction back out and
/// leaves a screen-linear interpolation — which is exactly what the PS1 did, because it had no
/// per-pixel divide. The result is the texture swimming and shearing on large triangles.
///
/// This is done with maths rather than a `noperspective` interpolator on purpose: GLSL ES has
/// no `noperspective`, so the modifier would silently do nothing on some Android devices and
/// the effect would be missing on exactly the platform hardest to test.
float3 PS1_PackAffineUV(float2 uv, float w)
{
    return float3(uv * w, w);
}

float2 PS1_UnpackAffineUV(float3 packed)
{
    return packed.xy / max(packed.z, 1e-5);
}

/// Blends between a correct UV and the affine one. 1 is period-accurate; lower values are how
/// you keep a big floor from making people seasick.
float2 PS1_MixUV(float2 correctUV, float3 packedAffine, float amount)
{
    return lerp(correctUV, PS1_UnpackAffineUV(packedAffine), saturate(amount));
}

/// Same trick for a world position, so triplanar surfaces warp too.
///
/// Without this, switching a material to triplanar silently switches the texture warping off:
/// the world position is perspective-correct by construction, so the projected coordinate never
/// shears. Since the kit walls are all triplanar, that would have removed the warp from every
/// wall in the building — the one surface it is most visible on.
float4 PS1_PackAffinePosition(float3 positionWS, float w)
{
    return float4(positionWS * w, w);
}

float3 PS1_MixPosition(float3 correctWS, float4 packedAffine, float amount)
{
    float3 affine = packedAffine.xyz / max(packedAffine.w, 1e-5);
    return lerp(correctWS, affine, saturate(amount));
}

// ---------------------------------------------------------------- triplanar projection

/// World-space triplanar sample.
///
/// Exists because the Blender kit pieces have no UVs authored for tiling textures, and giving
/// 31 pieces hand-made UVs is work that has to be redone for every new piece. Projecting from
/// the three world axes gives uniform texel density with no UVs at all, which is what lets the
/// kit walls be textured at all. Cost: three samples instead of one, and smearing on surfaces
/// that face none of the axes squarely — fine for a building made of boxes.
half4 PS1_SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS, float scale)
{
    float3 blend = pow(abs(normalWS), 4.0);
    blend /= max(blend.x + blend.y + blend.z, 1e-5);

    float invScale = 1.0 / max(scale, 1e-4);
    half4 x = SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * invScale);
    half4 y = SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * invScale);
    half4 z = SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * invScale);

    return x * blend.x + y * blend.y + z * blend.z;
}

// ---------------------------------------------------------------- macro variation

/// Cheap value noise on a world-space grid.
float PS1_ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    // Four corner hashes, bilinearly blended.
    float4 h = frac(sin(float4(
        dot(cell + float2(0, 0), float2(127.1, 311.7)),
        dot(cell + float2(1, 0), float2(127.1, 311.7)),
        dot(cell + float2(0, 1), float2(127.1, 311.7)),
        dot(cell + float2(1, 1), float2(127.1, 311.7)))) * 43758.5453);

    return lerp(lerp(h.x, h.y, f.x), lerp(h.z, h.w, f.x), f.y);
}

/// Large, slow brightness variation keyed to world position.
///
/// A single 2 m tile repeated across a 60 m building reads as wallpaper: the eye finds the
/// repeat immediately, and no amount of detail inside the tile hides it. Modulating brightness
/// on a scale much larger than the tile breaks the pattern for the cost of one noise sample,
/// and doubles as grime that pools in some parts of a corridor and not others.
half PS1_MacroVariation(float3 positionWS, float scale, float amount)
{
    if (amount <= 0.0) return 1.0;

    float n = PS1_ValueNoise(positionWS.xz / max(scale, 1e-3))
            * 0.6
            + PS1_ValueNoise(positionWS.xy / max(scale * 0.6, 1e-3)) * 0.4;

    return 1.0 - amount * (1.0 - n);
}

// ---------------------------------------------------------------- foliage movement

/// Sways a vertex in world space. Weighted by `weight` so a trunk base stays planted while the
/// canopy moves, and phase-shifted by world position so a wood does not wave in unison — which
/// reads as a single animated object rather than as wind.
float3 PS1_Sway(float3 positionWS, float weight, float strength, float speed)
{
    if (strength <= 0.0 || weight <= 0.0) return positionWS;

    float phase = positionWS.x * 0.35 + positionWS.z * 0.27;
    float t = _Time.y * speed;

    // Two frequencies so it does not read as a clean sine.
    float sway = sin(t + phase) * 0.7 + sin(t * 1.73 + phase * 2.1) * 0.3;

    positionWS.x += sway * strength * weight;
    positionWS.z += sway * strength * weight * 0.6;
    return positionWS;
}

#endif
