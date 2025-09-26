Shader "Unlit/NVENC/YFlip"
{
    Properties { _MainTex ("Tex", 2D) = "white" {} }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct v2f { float4 pos:SV_Position; float2 uv:TEXCOORD0; };
            v2f vert(uint id:SV_VertexID){
                float2 q = float2((id << 1) & 2, id & 2);
                v2f o; o.pos = float4(q * 2 - 1, 0, 1);
                o.uv  = float2(q.x, q.y);
                return o;
            }
            float4 frag(v2f i):SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); }
            ENDHLSL
        }
    }
}