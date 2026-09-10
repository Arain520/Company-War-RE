Shader "CompanyWarRE/CloudSeaURP"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _FlowMap ("Flow Map (RG)", 2D) = "gray" {}
        _LightColor ("Light Color", Color) = (0.82, 0.88, 0.91, 1)
        _ShadowColor ("Shadow Color", Color) = (0.39, 0.50, 0.59, 1)
        _Tiling ("Tiling", Float) = 3
        _UvOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _ScrollSpeed ("Scroll Speed", Vector) = (-0.004, 0.002, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 0.55
        _Density ("Density", Range(0, 1)) = 0.6
        _Softness ("Softness", Range(0.01, 0.5)) = 0.16
        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.16
        _FlowTiling ("Flow Tiling", Float) = 1
        _FlowStrength ("Flow Strength", Range(0, 0.25)) = 0.045
        _FlowSpeed ("Flow Speed", Range(0, 1)) = 0.08
        _DepthFadeDistance ("Depth Fade Distance", Float) = 14
        _UseVertexColor ("Use Vertex Color", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CloudSeaForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);
            float _CompanyWarCloudOpacityScale;

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _ShadowColor;
                float4 _ScrollSpeed;
                float4 _UvOffset;
                float _Tiling;
                float _Opacity;
                float _Density;
                float _Softness;
                float _EdgeFade;
                float _FlowTiling;
                float _FlowStrength;
                float _FlowSpeed;
                float _DepthFadeDistance;
                float _UseVertexColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 baseUv = input.uv * _Tiling + _UvOffset.xy + _Time.y * _ScrollSpeed.xy;
                float2 flowUv = input.uv * max(0.01, _FlowTiling) + _UvOffset.xy * 0.37;
                float2 flowVector = (SAMPLE_TEXTURE2D(
                    _FlowMap,
                    sampler_FlowMap,
                    flowUv).rg * 2.0 - 1.0) * _FlowStrength;
                float flowPhase = frac(_Time.y * _FlowSpeed);
                float flowBlend = abs(flowPhase * 2.0 - 1.0);
                float2 animatedUvA = baseUv + flowVector * flowPhase;
                float2 animatedUvB = baseUv + flowVector * frac(flowPhase + 0.5);
                half4 primarySample = SAMPLE_TEXTURE2D(
                    _NoiseTex,
                    sampler_NoiseTex,
                    animatedUvA);
                half4 flowedSample = SAMPLE_TEXTURE2D(
                    _NoiseTex,
                    sampler_NoiseTex,
                    animatedUvB);
                primarySample = lerp(primarySample, flowedSample, flowBlend);
                half primaryNoise = primarySample.r;
                half secondaryNoise = SAMPLE_TEXTURE2D(
                    _NoiseTex,
                    sampler_NoiseTex,
                    baseUv * 0.47 - _Time.y * _ScrollSpeed.yx * 0.61 + flowVector * 0.4).r;
                half cloudNoise = saturate(primaryNoise * 0.68h + secondaryNoise * 0.32h);
                half threshold = 1.0h - saturate(_Density);
                half coverage = smoothstep(
                    threshold - max(0.01h, (half)_Softness),
                    threshold + max(0.01h, (half)_Softness),
                    cloudNoise);

                float2 edgeUv = abs(input.uv * 2.0 - 1.0);
                half edgeDistance = (half)max(edgeUv.x, edgeUv.y);
                half edgeMask = 1.0h - smoothstep(
                    1.0h - max(0.001h, (half)_EdgeFade),
                    1.0h,
                    edgeDistance);
                half3 color = lerp(_ShadowColor.rgb, _LightColor.rgb, cloudNoise);
                color = MixFog(color, input.fogFactor);
                half4 vertexTint = lerp(half4(1, 1, 1, 1), input.color, _UseVertexColor);
                half textureShape = lerp(1.0h, primarySample.a, (half)_UseVertexColor);
                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawSceneDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawSceneDepth)
                    : LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float cloudEyeDepth = -TransformWorldToView(input.positionWS).z;
                half depthFade = _DepthFadeDistance > 0.001
                    ? saturate((sceneEyeDepth - cloudEyeDepth) / _DepthFadeDistance)
                    : 1.0h;
                half alpha = coverage * _Opacity * edgeMask * textureShape * vertexTint.a *
                             depthFade * max(0.0h, (half)_CompanyWarCloudOpacityScale);
                return half4(color * vertexTint.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
