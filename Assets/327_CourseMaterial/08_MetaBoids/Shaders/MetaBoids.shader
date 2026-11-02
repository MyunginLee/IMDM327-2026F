/*
 * DRAWING: each pixel adds the influence of all boid positions.
 * A high enough total becomes a visible metaball surface.
 * Read the frag function first. MetaBoidsStyle.hlsl only adds appearance.
 * Field idea: https://jamie-wong.com/2014/08/19/metaballs-and-marching-squares/
 * This lesson uses Gaussians and draws pixels directly, without marching squares.
 */
Shader "IMDM327/MetaBoids"
{
    Properties
    {
        _Radius ("Gaussian radius (field height)", Range(0.015, 0.08)) = 0.036
        _Threshold ("Surface threshold", Range(0.2, 2.0)) = 0.9
        _EdgeWidth ("Soft edge", Range(0.005, 0.15)) = 0.035
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            /*
             * 1. RECEIVE DATA FROM C#
             * _Boids is the latest position/velocity buffer from the compute shader.
             */
            StructuredBuffer<float4> _Boids;
            CBUFFER_START(UnityPerMaterial)
                float _Radius, _Threshold, _EdgeWidth;
                float2 _FieldSize;
                int _BoidCount, _Mode, _ShowCenters, _ShowGrid;
            CBUFFER_END
            #include "MetaBoidsStyle.hlsl"

            // Standard Unity setup: draw the quad and pass its UVs to each pixel.
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Close to a center: strong influence. Far away: weak influence.
            // This is the same Gaussian used in the 07_Metaballs example.
            float Influence(float2 offset)
            {
                return exp(-dot(offset, offset) / (2.0 * _Radius * _Radius));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * _FieldSize;
                float field = 0.0;
                float centers = 0.0;
                float2 gradient = 0.0;
                float3 mixedColor = 0.0;
                float3 dots = 0.0;
                float pixelWidth = max(length(fwidth(p)), 0.0003);

                /*
                 * 2. POSITIONS BECOME A FIELD
                 * Sample one Gaussian per boid and add them together.
                 * Overlapping influences can make a bridge between centers.
                 */
                [loop] for (int i = 0; i < _BoidCount; i++)
                {
                    float4 boid = _Boids[i];
                    float2 offset = p - boid.xy * _FieldSize;
                    float influence = Influence(offset);
                    field += influence;
                    mixedColor += BoidColor(i) * influence;
                    // The slope is only needed for the optional shiny lighting.
                    gradient -= offset * influence / (_Radius * _Radius);

                    if (_Mode == 0 || _ShowCenters == 1)
                    {
                        float dotMask = CenterMask(offset, pixelWidth);
                        centers = max(centers, dotMask);
                        float directionLine = VelocityMarker(offset, boid.zw, pixelWidth);
                        dots += BoidColor(i) * max(dotMask, directionLine);
                    }
                }

                /*
                 * 3. THE FIELD BECOMES A SURFACE
                 * Values above Threshold are inside. smoothstep softens the edge.
                 * This is the key metaball step; color and lighting are optional.
                 */
                float edge = max(_EdgeWidth, fwidth(field));
                float mask = smoothstep(_Threshold - edge, _Threshold + edge, field);
                float3 background = float3(0.0, 0.0, 0.0);
                float3 color = background;
                if (_Mode == 0) color += dots;
                else if (_Mode == 1)
                {
                    color = DensityColor(field, _Threshold, background);
                }
                else
                {
                    color = lerp(background, SurfaceColor(field, mixedColor, gradient), mask);
                }

                /*
                 * 4. OPTIONAL GUIDE MARKS
                 * Inspector checkboxes show the exact centers or the grid.
                 */
                if (_Mode != 0 && _ShowCenters == 1) color = lerp(color, float3(0.9, 1.0, 0.98), centers * 0.9);
                if (_ShowGrid == 1)
                {
                    color += GridMask(p) * 0.012;
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
