Shader "World Political Map 2D/Unlit Single Color Cursor" {
 
Properties {
    _Color ("Color", Color) = (1,1,1)
    _Orientation ("Orientation", Float) = 0 // 0 = horizontal, 1 = vertical
}
 
SubShader {
    Color [_Color]
        Tags {
        "Queue"="Transparent"
        "RenderType"="Transparent"
    }
    ZWrite Off
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				
		
		#include "UnityCG.cginc"
		
		fixed4 _Color;
		float _Orientation;

		struct appdata {
			float4 vertex : POSITION;
			float2 texcoord: TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
		};

        struct v2f {
			float4 pos     : SV_POSITION;
			float4 scrPos  : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        
        v2f vert(appdata v) {
		    v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
			o.scrPos = ComputeScreenPos(o.pos);
			return o;
		}
		
		fixed4 frag(v2f i) : COLOR {
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
			float2 wcoord = (i.scrPos.xy/i.scrPos.w);
			wcoord.x *= _ScreenParams.x;
			wcoord.y *= _ScreenParams.y;
			float wc = _Orientation==0 ? wcoord.x: wcoord.y;
			if ( fmod((int)(wc/4),2) )
				discard;
			return _Color;					
		}
			
		ENDCG
    }

}
 
}
