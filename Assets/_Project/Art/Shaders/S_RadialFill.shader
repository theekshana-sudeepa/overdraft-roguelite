// The bank ring (docs/04 §2, material #10). Radial fill computed from object-space angle, so
// the mesh needs no UVs and the CPU sets exactly one float per frame.
Shader "Overdraft/RadialFill"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1,0.773,0.231,1)
        _EmissionStrength ("Emission Strength", Float) = 2
        _Fill ("Fill", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "RadialFill"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 localXZ : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _EmissionStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.localXZ = IN.positionOS.xz;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Angle measured from +Z, sweeping clockwise, normalised to 0..1. Starting at
                // the top and filling clockwise matches how every progress ring the player has
                // ever seen behaves.
                float angle = atan2(IN.localXZ.x, IN.localXZ.y);
                float t = angle * 0.1591549431;      // 1 / (2*PI)
                t = t < 0.0 ? t + 1.0 : t;

                float fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);
                clip(fill - t);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float strength = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionStrength);

                return half4(baseColor.rgb * strength, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
