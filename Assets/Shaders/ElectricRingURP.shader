Shader "Custom/ElectricRingURP"
{
    Properties
    {
        _BaseMap   ("Texture (Ring VFX)", 2D) = "white" {}
        [HDR]_BaseColor ("Tint (HDR)", Color) = (0.3, 0.9, 1.5, 1)
        _Speed     ("Scroll Speed", Float) = 0.8
        _FlickerFreq ("Flicker Frequency", Float) = 12.0
        _FlickerAmp  ("Flicker Amplitude", Range(0,2)) = 0.35
        _Opacity   ("Opacity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "FORWARD_UNLIT"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Speed;
                float  _FlickerFreq;
                float  _FlickerAmp;
                float  _Opacity;
                float4 _BaseMap_ST;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // UV transform + scroll in X (positive = horário; negativo = anti-horário)
                float2 uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                uv += float2(_Time.y * _Speed, 0.0);
                OUT.uv = uv;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Flicker: 1 + (remap(sin) * amp)
                float s = sin(_Time.y * _FlickerFreq);     // -1..1
                float remap01 = s * 0.5 + 0.5;            // 0..1
                float flicker = 1.0 + remap01 * _FlickerAmp;

                half4 col = tex * _BaseColor * flicker;
                col.a = tex.a * _Opacity;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}