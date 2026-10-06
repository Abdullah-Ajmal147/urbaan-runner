// Soft round contact shadow (multiplies what is below it). Replaces realtime shadows on mobile (HANDOVER §3.11).
Shader "GameFactory/BlobShadow"
{
    Properties
    {
        _Strength ("Strength", Range(0,1)) = 0.6
        _Softness ("Softness", Range(0.5,4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Blob"
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Strength;
                half _Softness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half d = saturate(1 - length(i.uv * 2 - 1));
                half shade = 1 - _Strength * pow(d, 1.0 / _Softness);
                return half4(shade.xxx, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
