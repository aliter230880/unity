Shader "FORMA/LitVertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.42
        _Metallic ("Metallic", Range(0,1)) = 0
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 0.4
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.22
        [Toggle] _VERTEXCOLOR ("Vertex Color", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Back
        ZWrite On

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            fixed4 _Color;
            half _Glossiness;
            half _Metallic;
            half _BumpScale;
            half _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldN : TEXCOORD2;
                float4 color : COLOR;
                LIGHTING_COORDS(3,4)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                albedo.rgb *= i.color.rgb;
                clip(albedo.a - _Cutoff * 0.001);

                float3 n = normalize(i.worldN);
                float3 ldir = normalize(_WorldSpaceLightPos0.xyz);
                float3 lcol = _LightColor0.rgb;
                if (dot(lcol, lcol) < 0.0001)
                {
                    ldir = normalize(float3(0.35, 0.82, -0.45));
                    lcol = float3(1.15, 1.08, 0.98);
                }
                float ndl = saturate(dot(n, ldir));
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 h = normalize(ldir + view);
                float spec = pow(saturate(dot(n, h)), 8.0 + _Glossiness * 64.0) * _Glossiness;
                float3 ambient = ShadeSH9(float4(n, 1));
                float atten = LIGHT_ATTENUATION(i);
                float3 fill = float3(0.18, 0.17, 0.16);
                float fresnel = pow(1.0 - saturate(dot(n, view)), 3.0);
                float back = saturate(dot(-n, ldir));
                float3 subsurface = albedo.rgb * float3(1.0, 0.38, 0.24) * (back * 0.08 + fresnel * 0.035);
                float3 col = albedo.rgb * (ambient + fill + lcol * ndl * atten) + lcol * spec * atten + subsurface;
                return fixed4(col, albedo.a);
            }
            ENDCG
        }

        Pass
        {
            Name "SRPDEFAULTUNLIT"
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Glossiness;
            half _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldN : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 color : COLOR;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                albedo.rgb *= i.color.rgb;
                float3 n = normalize(i.worldN);
                float3 ldir = normalize(float3(0.4, 0.85, -0.5));
                float ndl = saturate(dot(n, ldir)) * 0.7 + 0.3;
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float spec = pow(saturate(dot(n, normalize(ldir + view))), 32.0) * _Glossiness;
                float3 col = albedo.rgb * ndl + spec * 0.35;
                return fixed4(col, albedo.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
