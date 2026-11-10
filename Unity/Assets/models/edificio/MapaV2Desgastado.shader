Shader "Five Nights/Mapa V2 Desgastado"
{
    Properties
    {
        _Color ("Color base", Color) = (.42,.36,.29,1)
        _Style ("Estilo (0 piso, 1 pared, 2 objeto)", Float) = 0
        _TileSize ("Ancho de mayólica en metros", Float) = .65
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input { float3 worldPos; float3 worldNormal; };
        fixed4 _Color;
        float _Style;
        float _TileSize;

        float hash(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float noise3(float3 p)
        {
            float3 cell = floor(p);
            float3 f = frac(p);
            f = f * f * (3 - 2 * f);
            float4 a = float4(hash(cell), hash(cell + float3(0, 1, 0)),
                             hash(cell + float3(0, 0, 1)), hash(cell + float3(0, 1, 1)));
            float4 b = float4(hash(cell + float3(1, 0, 0)), hash(cell + float3(1, 1, 0)),
                             hash(cell + float3(1, 0, 1)), hash(cell + float3(1, 1, 1)));
            float4 c = lerp(a, b, f.x);
            return lerp(lerp(c.x, c.y, f.y), lerp(c.z, c.w, f.y), f.z);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float3 p = input.worldPos;
            float broad = noise3(p * 1.45);
            float spots = noise3(p * 8.5);
            float grain = noise3(p * 62);
            float grime = smoothstep(.44, .77, .64 * broad + .36 * spots);
            float3 color;
            float smoothness = .13;
            if (_Style < .5)
            {
                float2 uv = p.xz / _TileSize + .27;
                float2 tile = floor(uv);
                float2 local = frac(uv);
                float2 edge = min(local, 1 - local);
                float edgeDistance = min(edge.x, edge.y);
                float aa = max(fwidth(edgeDistance), .002);
                float ceramic = smoothstep(.018 - aa, .018 + aa, edgeDistance);
                float checker = frac((tile.x + tile.y) * .5) * 2;
                float3 ceramicColor = lerp(float3(.09,.087,.079), float3(.76,.73,.66), checker);
                float chips = smoothstep(.73, .89, noise3(p * 26)) * (1 - smoothstep(.03, .12, edgeDistance));
                ceramicColor *= lerp(.84, 1.04, hash(float3(tile, 0))) * (1 - .57 * grime);
                color = lerp(float3(.21,.19,.16), ceramicColor, ceramic * (1 - chips));
                smoothness = lerp(.08, .28, ceramic) * (1 - grime * .6);
            }
            else if (_Style < 1.5)
            {
                float paintLine = smoothstep(1.04, 1.08, p.y + (spots - .5) * .05);
                float3 paint = lerp(float3(.23,.27,.24), float3(.58,.54,.45), paintLine);
                float streaks = smoothstep(.56, .84, noise3(p * float3(6.4,.62,6.4)));
                float peeling = smoothstep(.62, .77, broad + (spots - .5) * .32);
                float baseDirt = (1 - smoothstep(.08, .85, p.y)) * (.35 + .65 * spots);
                color = lerp(paint, float3(.25,.22,.18), peeling * .78);
                color *= 1 - saturate(.52 * grime + .27 * streaks + .47 * baseDirt);
            }
            else
            {
                float grainPattern = noise3(p * float3(8, 19, 1.6));
                float worn = smoothstep(.61, .8, spots + (grainPattern - .5) * .22);
                color = lerp(_Color.rgb, float3(.39,.33,.26), worn * .5);
                color *= (.72 + .45 * grainPattern) * (1 - .57 * grime);
            }
            output.Albedo = color * (.88 + .23 * grain);
            output.Smoothness = smoothness;
            output.Occlusion = 1 - .36 * grime;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
