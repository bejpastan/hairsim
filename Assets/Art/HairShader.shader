Shader "Custom/HairShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _Vertices;
            StructuredBuffer<int> _Indices;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct appdata
            {
                uint vertexID : SV_VertexID; 
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                uint vertexIndex = _Indices[v.vertexID];
                float3 positionOS = _Vertices[vertexIndex]; 
                
                float4 worldPosition = mul(unity_ObjectToWorld, float4(positionOS, 1.0));
                
                o.positionCS = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_V, worldPosition));
                
                o.color = _BaseColor;
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}