Shader "Fanhua/Paper Stone"
{
    Properties
    {
        _Color ("Stone", Color) = (1,1,1,1)
        _Mark ("Glyph (0 none, 1 fall, 2 danger, 3 step, 4 gate I, 5 gate II)", Float) = 0
        _Pressed ("Plate pressed", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Mark;
            float _Pressed;
            struct Varyings {
                float4 clip : SV_POSITION;
                float3 p : TEXCOORD0;
                float3 n : TEXCOORD1;
            };
            Varyings Vert(appdata_base v) {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.p = v.vertex.xyz;
                o.n = v.normal;
                return o;
            }
            float Band(float v, float c, float w) {
                return 1 - smoothstep(w, w + clamp(fwidth(v), .001, .025), abs(v-c));
            }
            fixed4 Frag(Varyings i) : SV_Target {
                float3 n = normalize(i.n);
                float top = saturate(n.y);
                float3 side = lerp(float3(.72,.64,.82),float3(.90,.80,.87),saturate(-n.z));
                float3 col = _Color.rgb * lerp(side, 1, top);
                // A scaled cube can span several board cells. Repeat the engraving
                // and bevel per cell, with seams anchored to the edges of the mesh.
                float3 size = float3(length(unity_ObjectToWorld._m00_m10_m20),
                                     length(unity_ObjectToWorld._m01_m11_m21),
                                     length(unity_ObjectToWorld._m02_m12_m22));
                float3 cells = max(1.0, floor(size + 0.5));
                float3 tile = frac(saturate(i.p + 0.5) * cells) - 0.5;
                float2 p = tile.xz;
                float2 panel = abs(n.y) > .99 ? tile.xz
                    : abs(n.x) > .99 ? tile.zy : tile.xy;
                float box = max(abs(p.x),abs(p.y));
                float v = p.y - abs(p.x) * .7;
                float fall = max(Band(v,-.17,.019),Band(v,.015,.019))
                    * (1-smoothstep(.16,.18,abs(p.x)));
                float danger = max(Band(p.x-p.y,0,.026),Band(p.x+p.y,0,.026))
                    * (1-smoothstep(.16,.18,box));
                float stepMark = Band(abs(p.x)+abs(p.y),.22,.019);
                float glyph = _Mark < .5 ? 0 : _Mark < 1.5 ? fall : _Mark < 2.5 ? danger : stepMark;
                float face = step(.99,top);
                float edge = max(abs(panel.x), abs(panel.y));
                // Shade both sides of each seam so long walls read as laid blocks.
                col *= 1 - smoothstep(.473, .499, edge) * .20;
                col = lerp(col, 1, Band(edge,.434,.009) * lerp(.15, .30, face));
                col = lerp(col, _Mark > 2.5 ? float3(.34,.23,.14) : float3(.99,.96,.91), glyph * face * .9);
                if (_Mark >= 3.5)
                {
                    // Repeat the engraved emblem on each face so it reads from either camera.
                    float rim = Band(edge, .39, .018);
                    float radius = length(panel);
                    float ring = Band(radius, .19, .018);
                    float first = 1 - smoothstep(.045, .065, length(panel - float2(.066, 0)));
                    float second = 1 - smoothstep(.045, .065, length(panel + float2(.066, 0)));
                    float single = 1 - smoothstep(.046, .065, radius);
                    float emblem = max(ring, _Mark < 4.5 ? single : max(first, second));
                    float3 brass = lerp(float3(.87,.74,.53), float3(1,.94,.70), _Pressed);
                    col = lerp(col, brass, max(rim, emblem) * .9);
                    float activeFace = (1 - smoothstep(.35, .38, edge)) * _Pressed;
                    col = lerp(col, brass, activeFace * .38);
                }
                return fixed4(col,1);
            }
            ENDCG
        }
    }
}
