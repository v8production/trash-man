Shader "Custom/AuraEffect"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.35, 0.9, 1, 0.35)
        _RimColor ("Rim Color", Color) = (0.65, 0.95, 1, 0.65)
        _Intensity ("Glow Intensity", Range(0, 5)) = 1.8
        _Alpha ("Alpha", Range(0, 1)) = 0.32
        _WaveScale ("Wave Scale", Range(0.1, 20)) = 7
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 1.6
        _Distortion ("Vertex Distortion", Range(0, 0.25)) = 0.045
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Aura"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _Intensity;
                float _Alpha;
                float _WaveScale;
                float _WaveSpeed;
                float _Distortion;
                float _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float time = _Time.y * _WaveSpeed;
                float wave = sin((IN.uv.y + time) * _WaveScale) * cos((IN.uv.x - time * 0.35) * _WaveScale);
                float3 positionOS = IN.positionOS.xyz + IN.normalOS * wave * _Distortion;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.normalWS = normalize(normalInputs.normalWS);
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y * _WaveSpeed;
                float verticalWave = sin((IN.uv.y * _WaveScale * 1.35) - time * 2.1) * 0.5 + 0.5;
                float radialWave = sin((IN.uv.x * _WaveScale * 2.0) + time) * 0.5 + 0.5;
                float shimmer = saturate(verticalWave * 0.65 + radialWave * 0.35);

                float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))), _RimPower);
                float3 color = lerp(_BaseColor.rgb, _RimColor.rgb, fresnel + shimmer * 0.25) * _Intensity;
                float alpha = saturate(_Alpha * (0.45 + shimmer * 0.35 + fresnel * 0.75));

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
