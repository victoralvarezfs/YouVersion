Shader "World Political Map 2D/Unlit Single Texture" {

Properties { 
    _MainTex ("Texture", 2D) = "white" {} 
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

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

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

            half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            
            // Apply shadows
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            float shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
            color.rgb *= shadowAttenuation;

            return color;
        }
        ENDHLSL
    }
}


	SubShader {
    Tags {
        "Queue"="Geometry"
        "RenderType"="Opaque"
    }
    Offset 50,50
	Pass {
		SetTexture[_MainTex]
		} 
	}
}