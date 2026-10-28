Shader "Custom/2D/MaskableGaussianBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
        
        // Required for UI Masking / Canvas support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        // Required for native Unity RectMask2D / Graphic Clipping support
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (0,0,0,0)
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // Added: Required properties for com.olegknyazev.softmask
        [HideInInspector] _SoftMask ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _SoftMask_Transform ("Mask Transform Matrix", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // Stencil settings block allows this shader to be masked by Unity's Mask components
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [ZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Compile variants for Unity's RectMask2D clipping engine
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            
            // Added: Compile variant for com.olegknyazev.softmask engine
            #pragma multi_compile __ SOFTMASK_ON

            #include "UnityCG.cginc"
            #include "UnityUI.cginc" // Gives access to UnityGet2DClipping math

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1; // Tracks position across canvas for RectMask2D / SoftMask
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // Automatically populated by Unity (x: 1/width, y: 1/height)
            float _BlurSize;

            // Uniforms populated at runtime by Unity Canvas layout component
            float4 _ClipRect;

            // Added: SoftMask plugin runtime buffers
            #ifdef SOFTMASK_ON
            sampler2D _SoftMask;
            float4 _SoftMask_Transform[2]; // Captures layout matrix from the parenting SoftMask component
            #endif

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex; // Capture pristine coordinates for clipping engines
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            // Simple 9-tap 2D Gaussian approximation wrapper
            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _BlurSize;
                
                // Gaussian kernel weights (Must sum to roughly 1.0)
                float weights[9] = { 0.05, 0.09, 0.12, 0.15, 0.16, 0.15, 0.12, 0.09, 0.05 };
                float offsets[9] = { -4.0, -3.0, -2.0, -1.0,  0.0,  1.0,  2.0,  3.0,  4.0 };

                fixed4 blurredColor = fixed4(0, 0, 0, 0);

                // Combined box-cross blur pass (Horizontal + Vertical combined for a 1-pass execution)
                for (int j = 0; j < 9; j++)
                {
                    // Sample horizontally
                    blurredColor += tex2D(_MainTex, i.texcoord + float2(offsets[j] * texel.x, 0.0)) * (weights[j] * 0.5);
                    // Sample vertically
                    blurredColor += tex2D(_MainTex, i.texcoord + float2(0.0, offsets[j] * texel.y)) * (weights[j] * 0.5);
                }

                // Apply vertex color tinting (crucial for UI or Sprite component compatibility)
                fixed4 finalColor = blurredColor * i.color;

                // Attenuate the pixel alpha value if it's placed inside a RectMask2D boundary
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                // Added: Sample SoftMask alpha data using world position transformation coordinates
                #ifdef SOFTMASK_ON
                float2 softMaskUV = float2(
                    i.worldPosition.x * _SoftMask_Transform[0].x + i.worldPosition.y * _SoftMask_Transform[0].y + _SoftMask_Transform[0].w,
                    i.worldPosition.x * _SoftMask_Transform[1].x + i.worldPosition.y * _SoftMask_Transform[1].y + _SoftMask_Transform[1].w
                );
                finalColor.a *= tex2D(_SoftMask, softMaskUV).a;
                #endif

                // Alpha discard fallback layer if canvas demands UI alpha clip optimization
                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
