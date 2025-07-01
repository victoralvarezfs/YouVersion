Shader "World Political Map 2D/Scenic 16K" {

	Properties {
		_TexTL ("Tex TL", 2D) = "white" {}
		_TexTR ("Tex TR", 2D) = "white" {}
		_TexBL ("Tex BL", 2D) = "white" {}
		_TexBR ("Tex BR", 2D) = "white" {}
		_NormalMap ("Normal Map", 2D) = "bump" {}
		_BumpAmount ("Bump Amount", Range(0, 1)) = 0.5
		_CloudMap ("Cloud Map", 2D) = "black" {}
		_CloudSpeed ("Cloud Speed", Range(-1, 1)) = -0.04
        _CloudAlpha ("Cloud Alpha", Range(0, 1)) = 0.7
        _CloudShadowStrength ("Cloud Shadow Strength", Range(0, 1)) = 0.3
        _CloudElevation ("Cloud Elevation", Range(0.001, 0.1)) = 0.003
        _SunLightDirection("Light Direction", Vector) = (0,0,1)        
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
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TexTL);
            TEXTURE2D(_TexTR);
            TEXTURE2D(_TexBL);
            TEXTURE2D(_TexBR);
            TEXTURE2D(_NormalMap);
            TEXTURE2D(_CloudMap);
            SAMPLER(sampler_TexTL);
            SAMPLER(sampler_TexTR);
            SAMPLER(sampler_TexBL);
            SAMPLER(sampler_TexBR);
            SAMPLER(sampler_NormalMap);
            SAMPLER(sampler_CloudMap);

            CBUFFER_START(UnityPerMaterial)
                float _BumpAmount;
                float _CloudSpeed;
                float _CloudAlpha;
                float _CloudShadowStrength;
                float _CloudElevation;
                float3 _SunLightDirection;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 tangentOS : TANGENT;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 tspace0 : TEXCOORD1;
                float3 tspace1 : TEXCOORD2;
                float3 tspace2 : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
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
                
                // Push back
                #if UNITY_REVERSED_Z
                    output.positionCS.z -= 0.0005;
                #else
                    output.positionCS.z += 0.0005;
                #endif

                output.uv = input.uv;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tspace0 = float3(normalInput.tangentWS.x, normalInput.bitangentWS.x, normalInput.normalWS.x);
                output.tspace1 = float3(normalInput.tangentWS.y, normalInput.bitangentWS.y, normalInput.normalWS.y);
                output.tspace2 = float3(normalInput.tangentWS.z, normalInput.bitangentWS.z, normalInput.normalWS.z);
                
                output.viewDir = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.positionWS = vertexInput.positionWS;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target {

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 earth;
                // compute Earth pixel color
                if (input.uv.y > 0.5) {
                    float2 uv = float2(input.uv.x * 2.0, input.uv.y * 1.9996);
                    if (uv.x < 1.0) {
                        earth = SAMPLE_TEXTURE2D_LOD(_TexTL, sampler_TexTL, uv, 0);
                    } else {
                        earth = SAMPLE_TEXTURE2D_LOD(_TexTR, sampler_TexTR, float2(uv.x - 1.0, uv.y), 0);
                    }
                } else {
                    float2 uv = float2(input.uv.x * 2.0, (input.uv.y - 0.5) * 2.002);
                    if (uv.x < 1.0) {
                        earth = SAMPLE_TEXTURE2D_LOD(_TexBL, sampler_TexBL, uv, 0);
                    } else {
                        earth = SAMPLE_TEXTURE2D_LOD(_TexBR, sampler_TexBR, float2(uv.x - 1.0, uv.y), 0);
                    }
                }

                half3 worldViewDir = normalize(input.viewDir);
                
                half3 tnormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                // transform normal from tangent to world space
                half3 worldNormal;
                worldNormal.x = dot(input.tspace0, tnormal);
                worldNormal.y = dot(input.tspace1, tnormal);
                worldNormal.z = dot(input.tspace2, tnormal);

                half LdotS = saturate(dot(_SunLightDirection, -normalize(worldNormal)));
                half wrappedDiffuse = LdotS * 0.5 + 0.5;
                earth.rgb *= wrappedDiffuse;
               
                float2 t = float2(_Time.x * _CloudSpeed, 0);
                float2 disp = -worldViewDir.xy * _CloudElevation;
                
                half3 cloud = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, input.uv + t - disp).rgb;
                half3 shadows = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, input.uv + t + float2(0.998, 0) + disp).rgb * _CloudShadowStrength;
                shadows *= saturate(dot(worldNormal, worldViewDir));
                half3 color = earth.rgb + (cloud.rgb - clamp(shadows.rgb, shadows.rgb, 1-cloud.rgb)) * _CloudAlpha;

                // Apply shadows to earth color
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                float atten = MainLightRealtimeShadow(shadowCoord);
                color.rgb *= atten;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }    
	
	Subshader {
		Tags { "RenderType"="Opaque" }
        Offset 50,50
            Pass {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag   
                #pragma target 3.0
                #include "UnityCG.cginc"

				sampler2D _TexTL;
				sampler2D _TexTR;
				sampler2D _TexBL;
				sampler2D _TexBR;
				sampler2D _NormalMap;
				sampler2D _CloudMap;
				float _BumpAmount;
				float _CloudSpeed;
				float _CloudAlpha;
				float _CloudShadowStrength;
				float _CloudElevation;
                float3 _SunLightDirection;
              				
                struct v2f {
                    float4 pos : SV_POSITION;
                    float2 uv: TEXCOORD0;
                    float3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
                    float3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
                    float3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z        
                    float3 viewDir: TEXCOORD4;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO                    
                };
                
                v2f vert (appdata_tan v) {
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
                    o.uv = v.texcoord;
                    half3 wNormal = UnityObjectToWorldNormal(v.normal);
                    half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                    // compute bitangent from cross product of normal and tangent
                    half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                    half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                    // output the tangent space matrix
                    o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                    o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                    o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
                    o.viewDir = UnityWorldSpaceViewDir(mul(unity_ObjectToWorld, v.vertex));  
                    return o;
                }
				
                half4 frag (v2f i) : SV_Target {

                    UNITY_SETUP_INSTANCE_ID(i);
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                    half4 earth;
					// compute Earth pixel color
					if (i.uv.y>0.5) {
						float2 uv = float2(i.uv.x * 2.0, i.uv.y * 1.9996);
						if (uv.x<1.0) {
							earth = tex2Dlod (_TexTL, float4(uv, 0, 0));
						} else {
							earth = tex2Dlod (_TexTR, float4(uv.x - 1.0, uv.y, 0, 0));
						}
					} else {
						float2 uv = float2(i.uv.x * 2.0, (i.uv.y - 0.5) * 2.002);
						if (uv.x<1.0) {
							earth = tex2Dlod (_TexBL, float4(uv, 0, 0));	
						} else {
							earth = tex2Dlod (_TexBR, float4(uv.x - 1.0, uv.y, 0, 0));	
						}
					}
                  half3 worldViewDir = normalize(i.viewDir);
                  
                  half3 tnormal = UnpackNormal(tex2D(_NormalMap, i.uv));
                  // transform normal from tangent to world space
                  half3 worldNormal;
                  worldNormal.x = dot(i.tspace0, tnormal);
                  worldNormal.y = dot(i.tspace1, tnormal);
                  worldNormal.z = dot(i.tspace2, tnormal);
                  half  LdotS = saturate(dot(_SunLightDirection, -normalize(worldNormal)));
                  half wrappedDiffuse = LdotS * 0.5 + 0.5;
                  earth.rgb *= wrappedDiffuse;
				
                  fixed2 t = fixed2(_Time[0] * _CloudSpeed, 0);
                  fixed2 disp = -worldViewDir * _CloudElevation;
                    
                  half3 cloud = tex2D (_CloudMap, i.uv + t - disp);
                  half3 shadows = tex2D (_CloudMap, i.uv + t + fixed2(0.998,0) + disp) * _CloudShadowStrength;
                  shadows *= saturate (dot(worldNormal, worldViewDir));
                  half3 color = earth.rgb + (cloud.rgb - clamp(shadows.rgb, shadows.rgb, 1-cloud.rgb)) * _CloudAlpha ;
                  return half4(color, 1.0);
                }
			

			ENDCG
        }
	}
}