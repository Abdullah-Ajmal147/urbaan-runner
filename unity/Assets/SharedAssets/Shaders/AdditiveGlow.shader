// Cheap additive glow for mobile: lantern halos (radial) and wet-road light reflections (streak).
// No textures; the falloff is computed from UVs.
Shader "GameFactory/AdditiveGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.7,0.3,1)
        [Enum(Radial,0,Streak,1)] _Shape ("Shape", Float) = 0
        _Softness ("Softness", Range(0.5,6)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Shape;
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
                float2 c = i.uv * 2 - 1;
                // Radial: soft disc. Streak: narrow across, fading along its length (a reflection on wet asphalt).
                half radial = saturate(1 - length(c));
                half streak = saturate(1 - abs(c.x)) * saturate(1 - abs(c.y) * 0.9);
                half a = pow(lerp(radial, streak, _Shape), _Softness);
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
