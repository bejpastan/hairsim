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
            StructuredBuffer<float4> _SegmentQuternion;
            int _Strands;
            int _Segments;

            float4 IDENTITY = float4(0,0,0,1);

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
                float3 positionOS = _Vertices[vertexIndex];

                int index = v.instanceID + (_Strands * (vertexIndex/4));

                uint first_segment_id = index -((max(index - _Strands + 1, 0) / max(index - _Strands + 1, 1)) * _Strands);
                float4 quaternion_1 = _SegmentQuternion[first_segment_id];
                float4 quaternion_2 = _SegmentQuternion[index];
                float4 quaternion = normalize(quaternion_1 + quaternion_2);

                float3 centre = _Positions[index];

                float3 t = 2.0 * cross(quaternion.xyz, positionOS);
                positionOS += quaternion.w * t + cross(quaternion.xyz, t);

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