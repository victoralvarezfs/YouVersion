Shader "World Political Map 2D/Unlit Texture 16K" {
 
Properties {
    _Color ("Color", Color) = (1,1,1)
	_TexTL ("Tex TL", 2D) = "white" {}
	_TexTR ("Tex TR", 2D) = "white" {}
	_TexBL ("Tex BL", 2D) = "white" {}
	_TexBR ("Tex BR", 2D) = "white" {}
}
 
SubShader {
    Tags { 
        "RenderType" = "Opaque"
        "RenderPipeline" = "UniversalPipeline"
        "Queue" = "Geometry"
    }
    Offset 50,50

    Pass {
        PackageRequirements {
            "com.unity.render-pipelines.universal":""
        }   

        Name "ForwardLit"
        Tags { "LightMode" = "UniversalForward" }
        
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _SHADOWS_SOFT

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_TexTL);
        TEXTURE2D(_TexTR);
        TEXTURE2D(_TexBL);
        TEXTURE2D(_TexBR);
        SAMPLER(sampler_TexTL);
        SAMPLER(sampler_TexTR);
        SAMPLER(sampler_TexBL);
        SAMPLER(sampler_TexBR);

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
        CBUFFER_END

        struct Attributes {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes input) {
            Varyings output = (Varyings)0;
            
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = vertexInput.positionCS;
            output.positionWS = vertexInput.positionWS;
            
            // Push back
            #if UNITY_REVERSED_Z
                output.positionCS.z -= 0.0005;
            #else
                output.positionCS.z += 0.0005;
            #endif

            output.uv = input.uv;
            return output;
        }

        half4 frag(Varyings input) : SV_Target {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 color;
            float mip = -8;

            // compute Earth pixel color
            if (input.uv.x < 0.5) {
                if (input.uv.y > 0.5) {
                    color = SAMPLE_TEXTURE2D_LOD(_TexTL, sampler_TexTL, float2(input.uv.x * 2.0, (input.uv.y - 0.5) * 2.0), mip);
                } else {
                    color = SAMPLE_TEXTURE2D_LOD(_TexBL, sampler_TexBL, float2(input.uv.x * 2.0, input.uv.y * 2.0), mip);
                }
            } else {
                if (input.uv.y > 0.5) {
                    color = SAMPLE_TEXTURE2D_LOD(_TexTR, sampler_TexTR, float2((input.uv.x - 0.5) * 2.0, (input.uv.y - 0.5) * 2.0), mip);
                } else {
                    color = SAMPLE_TEXTURE2D_LOD(_TexBR, sampler_TexBR, float2((input.uv.x - 0.5) * 2.0, input.uv.y * 2.0), mip);
                }
            }

            // Apply shadows
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            float shadowAtten = MainLightRealtimeShadow(shadowCoord);
            color.rgb *= shadowAtten * _Color.rgb;

            return color;
        }
        ENDHLSL
    }
}
 
SubShader {
  	Tags { "RenderType"="Opaque" }
		Offset 50,50
		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
				
				#include "UnityCG.cginc"
				
				sampler2D _TexTL;
				sampler2D _TexTR;
				sampler2D _TexBL;
				sampler2D _TexBR;

        
				struct appdata {
					float4 vertex : POSITION;
					float2 texcoord: TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};
				
				struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};
        
				v2f vert (appdata v) {
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_OUTPUT(v2f, o);
					UNITY_TRANSFER_INSTANCE_ID(v, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					o.pos = UnityObjectToClipPos(v.vertex);
                    // Push back
                    #if UNITY_REVERSED_Z
                        o.pos.z -= 0.0005;
                    #else
                        o.pos.z += 0.0005;
                    #endif
					o.uv				= v.texcoord;
					return o;
				 }
				
                fixed4 frag (v2f i) : SV_Target {
					UNITY_SETUP_INSTANCE_ID(i);
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
					fixed4 color;
                    float mip = -8;
                    // compute Earth pixel color
                    if (i.uv.x<0.5) {
                        if (i.uv.y>0.5) {
                            color = tex2Dlod(_TexTL, float4(i.uv.x * 2.0, (i.uv.y - 0.5) * 2.0, 0, mip));
                        } else {
                            color = tex2Dlod(_TexBL, float4(i.uv.x * 2.0, i.uv.y * 2.0, 0, mip));
                        }
                    } else {
                        if (i.uv.y>0.5) {
                            color = tex2Dlod(_TexTR, float4((i.uv.x - 0.5) * 2.0f, (i.uv.y - 0.5) * 2.0, 0, mip));
                        } else {
                            color = tex2Dlod(_TexBR, float4((i.uv.x - 0.5) * 2.0f, i.uv.y * 2.0, 0, mip));
                        }
                    }
                    return color;
                }
			
			ENDCG
		}
	}
}