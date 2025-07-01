Shader "World Political Map 2D/Unlit Surface Single Color" {
 
Properties {
    _Color ("Color", Color) = (1,1,1)
}
 
SubShader {
    ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha
    Tags {
        "Queue"="Geometry+1"
        "RenderType"="Transparent"
    	}
    Color  [_Color]
    Pass {
    }
}
 
}
