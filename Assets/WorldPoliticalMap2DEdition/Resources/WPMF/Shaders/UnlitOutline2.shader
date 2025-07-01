Shader "World Political Map 2D/Unlit Outline 2" {

Properties {
    _Color ("Color", Color) = (1,1,1,1)
}
 
SubShader {
    Tags {
       "Queue"="Geometry+301"
       "RenderType"="Opaque"
  	}
  	ZWrite Off

	CGINCLUDE

		#include "UnityCG.cginc"				

		fixed4 _Color;

		struct appdata {
			float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
		};


        struct v2f {
			float4 pos     : SV_POSITION;
			float2 uv      : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };


	ENDCG


    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			o.pos = UnityObjectToClipPos(v.vertex);
			#if UNITY_REVERSED_Z
				o.pos.z+= 0.001;
			#else
				o.pos.z-=0.001; //0.002; 
			#endif
			return o;
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;					
		}
			
		ENDCG
    }
    
   // SECOND STROKE ***********
 
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x += 2 * (o.pos.w/_ScreenParams.x);
			#if UNITY_REVERSED_Z
				o.pos.z+=0.001;
			#else
				o.pos.z-=0.001;
			#endif
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;
		}
			
		ENDCG
    }
    
      // THIRD STROKE ***********
 
	Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			
			o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y += 2 * (o.pos.w/_ScreenParams.y);
			#if UNITY_REVERSED_Z
				o.pos.z+=0.001;
			#else
				o.pos.z-=0.001;
			#endif
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;
		}
			
		ENDCG
    }
    
       
      // FOURTH STROKE ***********
 
  Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
								
			o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x -= 2 * (o.pos.w/_ScreenParams.x);
			#if UNITY_REVERSED_Z
			o.pos.z+=0.001;
			#else
			o.pos.z-=0.001;
			#endif
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;
		}
			
		ENDCG
    }
    
    // FIFTH STROKE ***********
 
	Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				
		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);					
			o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y -= 2 * (o.pos.w/_ScreenParams.y);
			#if UNITY_REVERSED_Z
				o.pos.z+=0.001;
			#else
				o.pos.z-=0.001;
			#endif
			return o;									
		}
		
		fixed4 frag(v2f i) : SV_Target {
			return _Color;
		}
			
		ENDCG
    }
    
}
}
