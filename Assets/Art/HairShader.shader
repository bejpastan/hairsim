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
            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float4> _Quternion;
            int _Segments;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct appdata
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
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
                float3 positionOS = _Vertices[vertexIndex];//local position of vertex from center of segment

                float4 quaternion = _Quternion[v.instanceID * (_Segments+1) + vertexIndex/4];
                float3 centre = _Positions[v.instanceID * (_Segments+1) + vertexIndex/4];

                float3 t = 2.0 * cross(quaternion.xyz, positionOS);
                positionOS += quaternion.w * t + cross(quaternion.xyz, t);


                //float4 worldPosition = mul(unity_ObjectToWorld, float4(positionOS, 1.0));
                // worldPosition.xyz += centre;
                float4 worldPosition = float4(positionOS + centre, 1.0);

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