// Instanced, half-Lambert lit crowd unit with a per-instance vertex bob.
// Built-in render pipeline, single ForwardBase pass. Loaded via Shader.Find("Swarmblastrr/CrowdInstanced").
Shader "Swarmblastrr/CrowdInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.55, 1.0, 1.0)
        _BobAmplitude ("Bob Amplitude", Float) = 0.08
        _BobSpeed ("Bob Speed", Float) = 10.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

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

            float _BobAmplitude;
            float _BobSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float instanceId = 0.0;
                #ifdef UNITY_INSTANCING_ENABLED
                    instanceId = (float)unity_InstanceID;
                #endif

                float4 vertex = v.vertex;
                vertex.y += sin(_Time.y * _BobSpeed + instanceId * 1.7) * _BobAmplitude * saturate(vertex.y);

                o.pos = UnityObjectToClipPos(vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 normal = normalize(i.worldNormal);

                // Directional light direction; falls back to straight up if no light is set.
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float lightLenSq = dot(lightDir, lightDir);
                float hasLight = step(1e-6, lightLenSq);
                lightDir = lerp(float3(0.0, 1.0, 0.0), lightDir * rsqrt(max(lightLenSq, 1e-6)), hasLight);

                float halfLambert = dot(normal, lightDir) * 0.5 + 0.5;
                float3 lighting = halfLambert * halfLambert + UNITY_LIGHTMODEL_AMBIENT.rgb;

                return fixed4(color.rgb * saturate(lighting), color.a);
            }
            ENDCG
        }
    }

    FallBack "Legacy Shaders/Diffuse"
}
