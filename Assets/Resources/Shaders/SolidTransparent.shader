// Swarmblastrr/SolidTransparent - alpha-blended, single-colour, half-Lambert lit, two-sided,
// GPU-instancing aware. Built-in render pipeline. Used by TrackVisuals.SolidMaterial for gate
// walls and other translucent markers; the alpha comes from _Color.a.
Shader "Swarmblastrr/SolidTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.5)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 n = normalize(i.worldNormal);
                float3 l = _WorldSpaceLightPos0.xyz;
                float len = length(l);
                l = len > 0.0001 ? l / len : float3(0.0, 1.0, 0.0);

                // Half-Lambert on the absolute normal so back faces (Cull Off) are lit like front faces.
                float nl = abs(dot(n, l)) * 0.5 + 0.5;
                float3 lighting = _LightColor0.rgb * nl + UNITY_LIGHTMODEL_AMBIENT.rgb;
                col.rgb *= lighting;
                return col;
            }
            ENDCG
        }
    }

    FallBack "Legacy Shaders/Diffuse"
}
