// Factory toon shader with an inverted-hull outline pass (obstacles, characters).
// Factory toon shader (URP, mobile): two-tone cel lighting from the main light, banded colour from
// additional lights (lanterns, neon), a rim light and an optional inverted-hull outline.
// Reusable by every game in Assets/SharedAssets.
Shader "GameFactory/ToonLitOutline"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.45,0.42,0.75,1)
        _RampThreshold ("Ramp Threshold", Range(-1,1)) = 0.1
        _RampSmooth ("Ramp Smoothness", Range(0.001,0.5)) = 0.04
        _RimColor ("Rim Color", Color) = (1,0.55,0.85,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,3)) = 0.9
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0.05,0.04,0.12,1)
        _OutlineWidth ("Outline Width", Range(0,0.05)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _RampThreshold;
            half _RampSmooth;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Band(half ndl) { return smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl); }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light main = GetMainLight();
                half lit = Band(dot(n, main.direction));
                half3 diffuse = lerp(_ShadowTint.rgb, 1.0h, lit) * main.color;
                diffuse += SampleSH(n) * 0.35h;

                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half band = Band(dot(n, l.direction));
                    diffuse += l.color * l.distanceAttenuation * (0.25h + 0.75h * band);
                }
                #endif

                half rim = pow(saturate(1.0h - dot(n, viewDir)), _RimPower) * _RimStrength;
                half3 color = albedo.rgb * diffuse + _RimColor.rgb * rim + _EmissionColor.rgb;
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Inverted hull outline: back faces pushed out along the normal, drawn in the outline colour.
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                posWS += nWS * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                if (_OutlineWidth <= 0) o.positionCS = float4(0, 0, -1, 1); // disabled: clip away
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fog), 1); }
            ENDHLSL
        }
    }
    FallBack Off
}
