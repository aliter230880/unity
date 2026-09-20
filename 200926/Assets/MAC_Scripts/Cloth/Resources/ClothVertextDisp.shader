Shader "Custom/ClothVertexDisp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BodyDepth ("Body Depth", 2D) = "white" {}
        _ClothingDepthTexture ("Cloth Depth", 2D) = "white" {}
        _DepthThreshold ("Depth Threshold", Float) = 0.01
        _DisplacementStrength ("Displacement Strength", Float) = 0.05
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

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _BodyDepth;
            sampler2D _ClothingDepthTexture;
            float _DepthThreshold;
            float _DisplacementStrength;

            v2f vert (appdata_full v)
            {
                v2f o;

                // 1. Получаем экранные UV для текущего вертекса
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float4 screenPos = ComputeScreenPos(clipPos);
                float2 uv = screenPos.xy / screenPos.w;

                // 2. Сэмплируем карты глубины (обязательно tex2Dlod в вершинном шейдере)[reference:6][reference:7]
                float bodyDepth = tex2Dlod(_BodyDepth, float4(uv, 0, 0)).r;
                float clothDepthTex = tex2Dlod(_ClothingDepthTexture, float4(uv, 0, 0)).r;
                
                // 3. Твоя проверенная логика (синяя и красная зоны)
                if (bodyDepth > 0 && clothDepthTex > 0 && bodyDepth > clothDepthTex)
                {
                    v.vertex.xyz += v.normal * _DisplacementStrength;
                    float diff = abs(clothDepthTex - bodyDepth);
                    // Если разница больше порога (синяя зона) — это рука, не трогаем
                    if (diff > _DepthThreshold)
                    {
                        // Здесь ничего не делаем, вершина остаётся на месте
                    }
                    // Если разница меньше порога (красная зона) — это выступающая часть тела
                    else
                    {
                        // 4. Смещаем вершину вперёд по её нормали
                        // v.vertex.xyz += v.normal * _DisplacementStrength;
                    }
                }

                // Преобразуем (возможно, смещённую) вершину обратно в Clip Space
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // return float4(bodyDepth, 0, 0, 1);
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}