Shader "Custom/SkyboxBlend6Sided"
{
    // Blend giữa 2 skybox dạng 6 mặt (Front/Back/Left/Right/Up/Down),
    // dùng để chuyển mượt giữa các mốc thời gian trong ngày (VD: skybox
    // Bình Minh -> skybox Trưa -> skybox Hoàng Hôn -> skybox Đêm).
    //
    // Set _Blend = 0  -> hiện hoàn toàn bộ A
    // Set _Blend = 1  -> hiện hoàn toàn bộ B
    // Set _Blend = 0.5 -> trộn 50/50 (dùng để crossfade theo thời gian)
    //
    // Cách dùng: Tạo 1 Material mới, chọn Shader "Custom/SkyboxBlend6Sided",
    // gán material này vào Lighting > Environment > Skybox Material.
    // Không cần set texture thủ công trong Inspector — script
    // GameDayLightingController sẽ tự SetTexture() lúc runtime.

    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _RotationA ("Rotation A", Range(0, 360)) = 0
        _RotationB ("Rotation B", Range(0, 360)) = 0
        _Blend ("Blend A -> B", Range(0, 1)) = 0

        [NoScaleOffset] _FrontA ("Front A [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackA  ("Back A [-Z]",  2D) = "grey" {}
        [NoScaleOffset] _LeftA  ("Left A [+X]",  2D) = "grey" {}
        [NoScaleOffset] _RightA ("Right A [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpA    ("Up A [+Y]",    2D) = "grey" {}
        [NoScaleOffset] _DownA  ("Down A [-Y]",  2D) = "grey" {}

        [NoScaleOffset] _FrontB ("Front B [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackB  ("Back B [-Z]",  2D) = "grey" {}
        [NoScaleOffset] _LeftB  ("Left B [+X]",  2D) = "grey" {}
        [NoScaleOffset] _RightB ("Right B [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpB    ("Up B [+Y]",    2D) = "grey" {}
        [NoScaleOffset] _DownB  ("Down B [-Y]",  2D) = "grey" {}

        // Hỗ trợ skybox dạng Cubemap (Skybox/Cubemap) bên cạnh dạng 6 Sided.
        // _UseCubeA / _UseCubeB: 0 = dùng 6 texture rời ở trên, 1 = dùng Cubemap.
        [NoScaleOffset] _CubeA ("Cubemap A", CUBE) = "" {}
        [NoScaleOffset] _CubeB ("Cubemap B", CUBE) = "" {}
        _UseCubeA ("Use Cubemap A", Float) = 0
        _UseCubeB ("Use Cubemap B", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FrontA, _BackA, _LeftA, _RightA, _UpA, _DownA;
            sampler2D _FrontB, _BackB, _LeftB, _RightB, _UpB, _DownB;
            samplerCUBE _CubeA;
            samplerCUBE _CubeB;
            half4 _Tint;
            half _Exposure;
            float _RotationA;
            float _RotationB;
            float _Blend;
            float _UseCubeA;
            float _UseCubeB;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float3 RotateY(float3 v, float degrees)
            {
                float rad = radians(degrees);
                float s = sin(rad);
                float c = cos(rad);
                return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Không xoay ở đây nữa vì A và B có thể có Rotation khác nhau
                // (lấy từ material gốc của từng skybox) -> xoay riêng ở fragment.
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 SampleFace(float3 dir,
                sampler2D front, sampler2D back,
                sampler2D left, sampler2D right,
                sampler2D up, sampler2D down)
            {
                float3 ad = abs(dir);
                float2 uv;
                half4 c;

                if (ad.x >= ad.y && ad.x >= ad.z)
                {
                    uv = dir.x > 0
                        ? float2(-dir.z, dir.y) / ad.x
                        : float2(dir.z, dir.y) / ad.x;
                    c = dir.x > 0 ? tex2D(left, uv * 0.5 + 0.5) : tex2D(right, uv * 0.5 + 0.5);
                }
                else if (ad.y >= ad.x && ad.y >= ad.z)
                {
                    uv = dir.y > 0
                        ? float2(dir.x, -dir.z) / ad.y
                        : float2(dir.x, dir.z) / ad.y;
                    c = dir.y > 0 ? tex2D(up, uv * 0.5 + 0.5) : tex2D(down, uv * 0.5 + 0.5);
                }
                else
                {
                    uv = dir.z > 0
                        ? float2(dir.x, dir.y) / ad.z
                        : float2(-dir.x, dir.y) / ad.z;
                    c = dir.z > 0 ? tex2D(front, uv * 0.5 + 0.5) : tex2D(back, uv * 0.5 + 0.5);
                }

                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 dirA = RotateY(dir, _RotationA);
                float3 dirB = RotateY(dir, _RotationB);

                half4 colA = _UseCubeA > 0.5
                    ? texCUBE(_CubeA, dirA)
                    : SampleFace(dirA, _FrontA, _BackA, _LeftA, _RightA, _UpA, _DownA);

                half4 colB = _UseCubeB > 0.5
                    ? texCUBE(_CubeB, dirB)
                    : SampleFace(dirB, _FrontB, _BackB, _LeftB, _RightB, _UpB, _DownB);

                half4 col = lerp(colA, colB, _Blend);
                col *= _Tint * 2;
                col.rgb *= _Exposure;

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
