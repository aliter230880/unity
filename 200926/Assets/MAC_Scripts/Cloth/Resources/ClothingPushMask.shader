Shader "Custom/ClothingPushMask"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _MaskTex ("Push Mask", 2D) = "black" {}
        _PushDistance ("Push Distance", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float _PushDistance;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;

                // Семплируем маску по UV (используем tex2Dlod, чтобы работало в вертексном шейдере)
                float mask = tex2Dlod(_MaskTex, float4(v.uv, 0, 0)).r;

                // Смещаем вершину вдоль объектной нормали
                float3 displacedPos = v.vertex.xyz + v.normal * mask * _PushDistance;

                // Передаём позицию в клиповое пространство
                o.vertex = UnityObjectToClipPos(displacedPos);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv) * _Color;
                return texCol;
            }
            ENDCG
        }
    }
}
