Shader "Custom/HairShader"
{
	Properties
	{
		_MyEntityScale("Entity Scale", Float) = 1.0	
	}
	SubShader
	{
		Tags{"RenderType"="Opaque"}
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing
			#pragma multi_compile_instancing _ DOTS_INSTANCING_ON

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
			};

			#ifdef UNITY_DOTS_INSTANCING_ENABLED
				UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
					UNITY_DOTS_INSTANCED_PROP(float, _MyEntityScale)
				UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
			#endif

			Varyings vert(Attributes input)
			{
				Varyings output;

				UNITY_SETUP_INSTANCE_ID(input)
				float entityScale = UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _MyEntityScale);

				float3 pos = input.positionOS;
				pos.x += entityScale;
				pos.x += sin((_Time.y+pos.y) * 0.5);
				VertexPositionInputs positionInputs = GetVertexPositionInputs(pos);
				output.positionHCS = positionInputs.positionCS;
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				return half4(1.0, 1.0, 1.0, 1.0);
			}
			ENDHLSL
		}
	}
}