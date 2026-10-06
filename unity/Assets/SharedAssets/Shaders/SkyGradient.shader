// Three-stop vertical sky gradient with a soft sun glow: one cheap skybox pass, no textures.
Shader "GameFactory/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.27,0.18,0.55,1)
        _Middle ("Middle", Color) = (0.98,0.45,0.55,1)
        _Bottom ("Horizon", Color) = (1,0.72,0.42,1)
        _MiddleHeight ("Middle height", Range(0.01,0.9)) = 0.22
        _SunColor ("Sun", Color) = (1,0.85,0.55,1)
        _SunDir ("Sun direction", Vector) = (0,0.12,1,0)
        _SunSize ("Sun size", Range(1,400)) = 60
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Middle, _Bottom, _SunColor;
                float4 _SunDir;
                half _MiddleHeight, _SunSize;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half y = saturate(d.y);
                half3 c = y < _MiddleHeight ? lerp(_Bottom.rgb, _Middle.rgb, y / _MiddleHeight)
                                            : lerp(_Middle.rgb, _Top.rgb, saturate((y - _MiddleHeight) / (1 - _MiddleHeight)));
                half sun = pow(saturate(dot(d, normalize(_SunDir.xyz))), _SunSize);
                c += _SunColor.rgb * sun;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
