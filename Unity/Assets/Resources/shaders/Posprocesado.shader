// Posprocesado del juego (pipeline integrado, una pasada final más tres chicas
// para el bloom): viñeta, grano, aberración cromática, bloom, color frío y
// desaturado, y la estática de la pantalla (líneas, nieve y temblor horizontal)
// que se pide con AmbienteDelJuego.PedirEstatica. Lo aplica Posprocesado.cs.
Shader "Five Nights/Posprocesado"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _Brillo;
        float _Umbral;
        float _IntensidadBloom;
        float _Vineta;
        float _Grano;
        float _Aberracion;
        float _Estatica;
        float _Tiempo;
        float _Desaturacion;
        float4 _Tinte;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }

        float ruido(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Desenfoque gaussiano de 9 muestras en una dirección (en píxeles de la textura chica).
        fixed4 desenfocar(float2 uv, float2 direccion)
        {
            float pesos[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };
            fixed4 suma = tex2D(_MainTex, uv) * pesos[0];
            for (int i = 1; i < 5; i++)
            {
                float2 paso = direccion * _MainTex_TexelSize.xy * (i * 1.5);
                suma += tex2D(_MainTex, uv + paso) * pesos[i];
                suma += tex2D(_MainTex, uv - paso) * pesos[i];
            }
            return suma;
        }
        ENDCG

        // 0: lo que brilla más que el umbral (se dibuja en una textura a 1/4).
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float luz = max(c.r, max(c.g, c.b));
                float factor = max(0.0, luz - _Umbral) / max(luz, 1e-4);
                return fixed4(c.rgb * factor, 1);
            }
            ENDCG
        }

        // 1: desenfoque horizontal.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return desenfocar(i.uv, float2(1, 0)); }
            ENDCG
        }

        // 2: desenfoque vertical.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return desenfocar(i.uv, float2(0, 1)); }
            ENDCG
        }

        // 3: la imagen final.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Estática: la imagen tiembla a lo ancho por franjas.
                float salto = _Estatica * (ruido(float2(floor(uv.y * 60.0), floor(_Tiempo * 20.0))) - 0.5) * 0.08;
                uv.x = saturate(uv.x + salto);

                float2 d = uv - 0.5;
                float2 ab = d * _Aberracion * (1.0 + 2.0 * _Estatica);
                float r = tex2D(_MainTex, saturate(uv + ab)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, saturate(uv - ab)).b;
                float3 c = float3(r, g, b);

                c += tex2D(_Brillo, uv).rgb * _IntensidadBloom;

                // Color frío y algo desaturado, como una cámara de vigilancia.
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, lum.xxx, _Desaturacion) * _Tinte.rgb;

                // Viñeta: 1 en el centro, oscuro en los bordes.
                float vineta = smoothstep(0.95, 0.35, length(d) * 1.25);
                c *= lerp(1.0, vineta, _Vineta);

                // Grano: proporcional a la luz, para que lo oscuro no parezca nieve de TV.
                float grano = ruido(uv * 1000.0 + frac(_Tiempo) * 100.0) - 0.5;
                c += grano * _Grano * (0.25 + 0.75 * saturate(lum));

                // Estática: líneas que bajan y nieve.
                float lineas = 0.5 + 0.5 * sin(uv.y * 900.0 + _Tiempo * 30.0);
                float nieve = ruido(uv * 700.0 + _Tiempo * 50.0);
                // Nunca al 100 %: aun en el pico se adivina la imagen detrás de la nieve.
                c = lerp(c, c * (0.5 + 0.5 * lineas) + nieve * 0.45, _Estatica * 0.75);

                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
