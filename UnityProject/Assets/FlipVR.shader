Shader "Unlit/FlipVR"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType"="Overlay"
            "Queue"="Overlay"
        }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        GrabPass { }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = float4(v.vertex.xy * 2, 0, 1);
                float4 pos = o.pos;
                pos.x *= -1;
                o.grabPos = ComputeGrabScreenPos(pos);
                return o;
            }

            bool IsInMirror()
            {
                return unity_CameraProjection[2][0] != 0.f || unity_CameraProjection[2][1] != 0.f;
            }

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_GrabTexture); 

            half4 frag(v2f i) : SV_Target
            {
                if (IsInMirror()) discard;
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(UNITY_STEREO_INSTANCING_ENABLED)
                    unity_StereoEyeIndex.x = 1 - i.stereoTargetEyeIndex;
                #endif
                half4 bgcolor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture, i.grabPos);
                return bgcolor;
            }
            ENDCG
        }
    }
}