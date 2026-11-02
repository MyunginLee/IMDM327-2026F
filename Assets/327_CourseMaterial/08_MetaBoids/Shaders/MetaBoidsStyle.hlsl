#ifndef META_BOIDS_STYLE_INCLUDED
#define META_BOIDS_STYLE_INCLUDED

/*
 * OPTIONAL DISPLAY HELPERS
 * These functions only choose colors, highlights, and guide lines.
 * Skip this file while learning the boids rules and Gaussian field.
 */

// Repeat three colors across the boids.
float3 BoidColor(int index)
{
    if (index % 3 == 0) return float3(0.15, 0.85, 0.65);
    if (index % 3 == 1) return float3(0.22, 0.55, 1.0);
    return float3(1.0, 0.48, 0.25);
}

// A small dot at a boid's exact position.
float CenterMask(float2 offset, float pixelWidth)
{
    return 1.0 - smoothstep(0.004, 0.004 + pixelWidth, length(offset));
}

// A short line pointing in the boid's velocity direction.
float VelocityMarker(float2 offset, float2 velocity, float pixelWidth)
{
    float2 tip = velocity * 0.12;
    float alongLine = saturate(dot(offset, tip) / max(dot(tip, tip), 0.000001));
    float distanceToLine = length(offset - tip * alongLine);
    return (1.0 - smoothstep(0.001, 0.001 + pixelWidth, distanceToLine)) * 0.7;
}

// Show field strength as a heat map, with a white threshold contour.
float3 DensityColor(float field, float threshold, float3 background)
{
    float heat = 1.0 - exp(-field * 0.7);
    float3 color = lerp(background, float3(0.10, 0.75, 0.65), heat);
    color = lerp(color, float3(1.0, 0.65, 0.28), saturate(heat * 2.0 - 1.0));
    float contourWidth = max(fwidth(field) * 1.5, 0.015);
    float contour = 1.0 - smoothstep(0.0, contourWidth, abs(field - threshold));
    return lerp(color, float3(0.95, 1.0, 0.9), contour * 0.8);
}

// Use the field's slope to make a flat shape look shiny. No 3D mesh is created.
float3 SurfaceColor(float field, float3 weightedColor, float2 gradient)
{
    float3 baseColor = weightedColor / max(field, 0.00001);
    float3 normal = normalize(float3(-gradient * 0.045, 1.0));
    float3 light = normalize(float3(-0.5, 0.7, 1.0));
    float diffuse = 0.4 + 0.6 * saturate(dot(normal, light));
    float3 halfway = normalize(light + float3(0, 0, 1));
    float highlight = pow(saturate(dot(normal, halfway)), 40.0);
    return baseColor * diffuse + highlight * 0.3;
}

// fwidth measures about one screen pixel, keeping the grid lines thin.
float GridMask(float2 samplePosition)
{
    float2 grid = abs(frac(samplePosition * 10.0 + 0.5) - 0.5) / max(fwidth(samplePosition * 10.0), 0.001);
    return 1.0 - saturate(min(grid.x, grid.y));
}

#endif
