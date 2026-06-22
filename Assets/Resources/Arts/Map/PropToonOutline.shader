Shader "Custom/PropToonOutline"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Texture (Hand-painted)", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1,1,1,1)

        [Header(Cel Shading)]
        _ShadowColor ("Shadow Color", Color) = (0.55,0.55,0.65,1)
        _ShadowThreshold ("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowSmoothness ("Shadow Edge Softness", Range(0.001,0.5)) = 0.02

        [Header(Outline)]
        _OutlineOn ("Outline On (0=Off 1=On)", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 0.02
        _OutlineDotBias ("Outline Dot Bias (complex mesh correction)", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        // ===================================================
        // PASS 1 : Cel Shaded Base (본체) - 반드시 Outline보다 먼저 그려서
        //          깊이 버퍼를 먼저 채워야 한다. 순서가 바뀌면 이너라인이 생긴다.
        // ===================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // ===== 임시 디버그: Normal 방향을 색으로 확인 =====
                // 표면 방향에 따라 색이 자연스럽게 바뀌면 정상, 표면 전체가 같은 색이거나
                // 칠해진 색이 위치와 안 맞으면 노멀 데이터에 문제가 있는 것.
                return float4(normalize(IN.normalWS) * 0.5 + 0.5, 1);

                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float4 albedo = tex * _BaseColor;

                Light mainLight = GetMainLight();
                float3 N = normalize(IN.normalWS);
                float3 L = normalize(mainLight.direction);

                float NdotL = dot(N, L);
                // Cel Shading: smoothstep으로 명암을 부드럽게 섞지 않고 계단처럼 끊어줌
                float lit = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);

                float3 finalColor = lerp(_ShadowColor.rgb * albedo.rgb, albedo.rgb, lit);

                return float4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ===================================================
        // PASS 2 : Outline (본체 다음에 그림, ZTest Greater로
        //          본체보다 "더 멀리" 있는 부분만 그려서 이너라인 제거)
        // ===================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest Greater   // 본체 깊이보다 더 뒤(카메라에서 먼)인 픽셀만 그림 = 안쪽 면 노출 방지

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineOn;
                float _OutlineDotBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // OutlineOn이 0이면 두께를 0으로 만들어서 본체에 완전히 겹쳐지게 함 (= 안 보임)
                float width = _OutlineWidth * _OutlineOn;

                // 표준 인버트헐(Inverted Hull) 방식: 버텍스 노멀 방향으로만 부풀린다.
                // 각도/스케일에 따라 흔들리는 문제는 메쉬 노멀 자체를 정리해야 근본적으로 해결되고,
                // 여기서는 "이너라인 제거"라는 더 큰 문제부터 먼저 확실히 잡는다.
                float3 nrmDir = normalize(IN.normalOS);
                float3 offset = nrmDir * width;
                float3 posOS = IN.positionOS.xyz + offset;

                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // 그림자 받기/주기에 필요한 기본 패스
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
