#ifndef PLAYER_WITH_GFX_RED_ARMOR_INPUT
#define PLAYER_WITH_GFX_RED_ARMOR_INPUT

// Keep URP's normal lit surface, replacing only the cyan/blue paint colour.
// The source texture and all UVs, shadows, normals and material detail stay intact.
#define InitializeStandardLitSurfaceData InitializeOriginalSoldierSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    InitializeOriginalSoldierSurfaceData(uv, surfaceData);
    half3 original = surfaceData.albedo;
    half blueMask = smoothstep(0.018h, 0.10h, original.b - original.r)
                  * smoothstep(0.008h, 0.055h, original.g - original.r);
    half value = max(original.r, max(original.g, original.b));
    half3 redPaint = value * half3(1.0h, 0.22h, 0.18h);
    surfaceData.albedo = lerp(original, redPaint, blueMask);
}
#endif
