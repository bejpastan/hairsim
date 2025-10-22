Shader "Custom/HairShader"
{
	Properties{}
	SubShader
	{
		Tags{"RenderType"="Opaque"}
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing _ DOTS_INSTANCING_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;

				float3 pos = input.positionOS;
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