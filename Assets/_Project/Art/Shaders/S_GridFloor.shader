// Procedural arena floor (docs/04 §2, material #2). No texture, no lightmap, no lights.
// Lines are derived from world XZ so the grid never stretches with the quad's scale.
Shader "Overdraft/GridFloor"
{
    Properties
    {
        _BaseColor ("Void Color", Color) = (0.027,0.027,0.047,1)
        _GridColor ("Grid Color", Color) = (0.078,0.110,0.180,1)
        _GridSpacing ("Grid Spacing", Float) = 2
        _GridLineWidth ("Grid Line Width", Float) = 0.03
        _Radius ("Arena Radius", Float) = 18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Grid"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GridColor;
                float _GridSpacing;
                float _GridLineWidth;
                float _Radius;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 gridUV = IN.positionWS.xz / max(_GridSpacing, 0.0001);
                float2 distanceToLine = abs(frac(gridUV) - 0.5);

                // Screen-space derivative keeps the line one pixel wide at every zoom, which is
                // what stops the grid shimmering when the camera punches.
                float2 fw = fwidth(gridUV);
                float2 line2 = smoothstep(_GridLineWidth + fw, _GridLineWidth - fw, distanceToLine);
                float lineMask = saturate(max(line2.x, line2.y));

                float3 color = lerp(_BaseColor.rgb, _GridColor.rgb, lineMask);

                // Fade the grid out past the arena boundary so the playfield edge reads without
                // needing a wall mesh.
                float distanceFromCenter = length(IN.positionWS.xz);
                float edgeFade = 1.0 - smoothstep(_Radius * 0.92, _Radius, distanceFromCenter);
                color *= edgeFade;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
