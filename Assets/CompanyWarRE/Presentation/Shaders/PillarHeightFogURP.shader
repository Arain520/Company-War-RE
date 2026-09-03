Shader "CompanyWarRE/PillarHeightFogURP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.32, 0.38, 0.46, 1)
        _FogColor ("Height Fog Color", Color) = (0.56, 0.66, 0.72, 1)
        _FogTopY ("Fog Top World Y", Float) = 8
        _FogBottomY ("Fog Bottom World Y", Float) = -24
        _FogStrength ("Fog Strength", Range(0, 1)) = 0.96
        _FadeCurve ("Fade Curve", Range(0.25, 4)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PillarForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FogColor;
                float _FogTopY;
                float _FogBottomY;
                float _FogStrength;
                float _FadeCurve;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half lighting = 0.38h + diffuse * mainLight.shadowAttenuation * 0.62h;
                half3 originalColor = _BaseColor.rgb * lighting * mainLight.color;

                float fogRange = max(0.001, _FogTopY - _FogBottomY);
                half heightFog = saturate((_FogTopY - input.positionWS.y) / fogRange);
                heightFog = pow(heightFog, max(0.25h, (half)_FadeCurve));
                half3 color = lerp(
                    originalColor,
                    _FogColor.rgb,
                    saturate(heightFog * _FogStrength));
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
