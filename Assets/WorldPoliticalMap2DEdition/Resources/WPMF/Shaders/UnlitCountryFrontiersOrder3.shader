Shader "World Political Map 2D/Unlit Country Frontiers Order 3" {
 
Properties {
    _Color ("Color", Color) = (0,1,0,1)
    _OuterColor("Outer Color", Color) = (0,0.8,0,0.8)
}
 
SubShader {
	LOD 300
    Tags {
        "Queue"="Geometry+300"
        "RenderType"="Opaque"
    	}
 	Blend SrcAlpha OneMinusSrcAlpha


	CGINCLUDE

		#include "UnityCG.cginc"		

		struct appdata {
			float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
		};

        struct v2f {
			float4 pos     : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

		fixed4 _Color;


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
				o.pos.z+= 0.00001; //0.002; 
			#else
				o.pos.z-=0.00001; //0.002; 
			#endif
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
		
    }
}

SubShader {
	LOD 200
    Tags {
        "Queue"="Geometry+300"
        "RenderType"="Opaque"
    	}
    Blend SrcAlpha OneMinusSrcAlpha
    
    Pass { // (+1,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag		
		#include "UnityCG.cginc"		

		fixed4 _OuterColor;

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x += 1.25 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color * 0.5 + _OuterColor * 0.5;					
		}
		ENDCG
		
    }
   Pass { // (-1,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag		
		#include "UnityCG.cginc"		

		fixed4 _OuterColor;
		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x -= 1.25 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color * 0.5 + _OuterColor * 0.5;					
		}
		ENDCG
		
    }    
  Pass { // (0,-1)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag		
		#include "UnityCG.cginc"		

		fixed4 _OuterColor;

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color * 0.5 + _OuterColor * 0.5;					
		}
		ENDCG
    } 
           
    Pass { // (0, +1)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag		
		#include "UnityCG.cginc"		

		fixed4 _OuterColor;
		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y += 1.25 * (v.vertex.w/_ScreenParams.y);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color * 0.5 + _OuterColor * 0.5;					
		}
		ENDCG
    }    
    
     Pass {
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag	
		#include "UnityCG.cginc"			

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			#if UNITY_REVERSED_Z
				o.pos.z+= 0.00002; //0.002; 
			#else
				o.pos.z-=0.00002; //0.002; 
			#endif
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
		
    }  
}

SubShader {
	LOD 100
    Tags {
        "Queue"="Geometry+300"
        "RenderType"="Opaque"
    	}
    Blend SrcAlpha OneMinusSrcAlpha
  
    
            
    Pass { // (+2,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		fixed4 _OuterColor;

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x += 2.75 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _OuterColor;					
		}
		ENDCG
    }
                      
    Pass { // (0,+2)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		fixed4 _OuterColor;
		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y += 2.75 * (v.vertex.w/_ScreenParams.y);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _OuterColor;					
		}
		ENDCG
		
    } 
    
  	Pass { // (-2,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		fixed4 _OuterColor;

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x -= 2.75 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _OuterColor;					
		}
		ENDCG
		
    }     
    
        
  Pass { // (0,-2)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		fixed4 _OuterColor;

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y -= 2.75 * (v.vertex.w/_ScreenParams.y);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _OuterColor;					
		}
		ENDCG
    }            
       
       
        
    Pass { // (+1,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x += 1.25 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
		
    }
    

    
    
   Pass { // (-1,0)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.x -= 1.25 * (v.vertex.w/_ScreenParams.x);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;			
		}
		ENDCG
		
    }   
    
     
       
  Pass { // (0,-1)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
 			o.pos.y -= 1.25 * (v.vertex.w/_ScreenParams.y);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
    } 
 
                                
     Pass { // (0,+1)
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.y += 1.25 * (v.vertex.w/_ScreenParams.y);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
    }    

    
    Pass {
	   	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag
		#include "UnityCG.cginc"				

		
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			#if UNITY_REVERSED_Z
				o.pos.z+= 0.00002; //0.002; 
			#else
				o.pos.z-=0.00002; //0.002; 
			#endif
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
			return _Color;					
		}
		ENDCG
		
    }  
    
}
 
}
