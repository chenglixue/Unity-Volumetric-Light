Shader "Elysia/S_PBR"
{
    Properties
    {
        [Header(Rendering Setting)]
        [Space(10)]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", int) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc("Blend Source", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendDst("Blend Destination", int) = 0
        [Enum(UnityEngine.Rendering.BlendOp)]_BlendOp("Blend Operator", int) = 0
        [Enum(Off, 0, On, 1)] _ZWriteEnable("ZWrite Mode", int) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestCompare("ZTest Mode", int) = 4
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask("Color Mask", Int) = 15
        [Space(10)]
        [IntRange] _StencilRef("Stencil Ref", Range(0, 255)) = 0
        [IntRange] _StencilReadMask("Stencil Read Mask", Range(0, 255)) = 255
        [IntRange] _StencilWriteMask("Stencil Write Mask", Range(0, 255)) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilTestCompare("Stencil Test Compare", Int) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassOp("Stencil Pass Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp("Stencil Fail Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilDepthFailOp("Stencil Depth Test Fail Operator", Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilBackTestCompare("Stencil Back Test Compare", Int) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilBackPassOp("Stencil Back Pass Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilBackFailOp("Stencil Back Fail Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilBackDepthFailOp("Stencil Back Depth Fail Operator", Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilFrontTestCompare("Stencil Front Test Compare", Int) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFrontPassOp("Stencil Front Pass Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFrontFailOp("Stencil Front Fail Operator", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFrontDepthFailOp("Stencil Front Depth Fail Operator", Int) = 0
        [KeywordEnum(Unlit, DefaultLit, Disney, Subsurface, Hair, Cloth, Clear Coat)] _ShadingModel("Shading Model", Float) = 0
        [Space(10)]
        
        [Toggle] _Enable_Albedo             ("Enable Albedo", Int)           = 0
        [Toggle] _Enable_Normal             ("Enable Normal", Int)           = 0
        [Toggle] _Enable_Metallic           ("Enable Metallic", Int)         = 0
        [Toggle] _Enable_Roughness          ("Enable Roughness", Int)        = 0
        [Toggle] _Enable_AO                 ("Enable AO", Int)               = 0
        [Toggle] _Enable_Emission           ("Enable Emission", Int)         = 0
        
        [HDR]_Albedo                        ("Albedo Value", Color)          = (1, 1, 1, 1)
        _Normal                             ("Normal Value", Vector)         = (0.5, 0.5, 1, 1)
        _Metallic                           ("Metallic Value", Range(0, 1))  = 0
        _Roughness                          ("Roughness Value", Range(0, 1)) = 0
        _AO                                 ("AO Value", Range(0, 1))        = 1
        _Emission                           ("Emission Value", Color)        = (1, 1, 1, 1)
        _Subsurface                         ("Subsurface", Range(0,1))       = 1
        _Specular                           ("Specular"  , Range(0,1))       = 1
        _SpecularTint                       ("SpecularTint", Range(0,1))     = 1
        _Anisotropic                        ("Anisotropic", Range(0,1))      = 1
        _Sheen                              ("Sheen", Range(0,1))            = 1
        _SheenTint                          ("SheenTint", Range(0,1))        = 1
        _Clearcoat                          ("Clearcoat", Range(0,1))        = 1
        _ClearcoatGloss                     ("ClearcoatGloss", Range(0,1))   = 1
        
        [MainTex] _AlbedoTex            ("Albedo Tex",    2D)                           = "white" {}
        [Normal]  _NormalTex            ("Normal Tex",    2D)                           = "bump"  {}
        [NoScaleOffset]          _MetallicTex          ("Metallic Tex",  2D)                           = "black" {}
        [NoScaleOffset]          _RoughnessTex         ("Roughness Tex", 2D)                           = "white" {}
        [NoScaleOffset]          _AOTex                ("AO Tex",        2D)                           = "white" {}
        [NoScaleOffset]          _SpecularTex          ("Specular Tex",  2D)                           = "black" {}
        [NoScaleOffset]          _EmissionTex          ("Emission Tex",  2D)                           = "black" {}
        [NoScaleOffset]          _DiffuseIBLTex        ("Diffuse IBL Tex",  Cube)                      = "black" {}
        [NoScaleOffset]          _SpecularIBLTex       ("Specular IBL Tex",  Cube)                     = "black" {}
        [NoScaleOffset]          _SpecularFactorLUTTex ("Specular Factor LUT Tex",  2D)                = "black" {}
        
        
        _Cutoff                             ("Cut off", Range(0, 1))         = 0.5
        _AlbedoTint                         ("Albedo Tint", Color)           = (1,1,1,1)
        _NormalIntensity                    ("Normal Intensity", Range(0, 1))= 1
    }
    
    SubShader
    {
        Cull [_CullMode]
        Blend [_BlendSrc] [_BlendDst]
        BlendOp [_BlendOp]
        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
            
            Comp [_StencilTestCompare]
            Pass [_StencilPassOp]
            Fail [_StencilFailOp]
            ZFail [_StencilDepthFailOp]
            
            CompBack [_StencilBackTestCompare]
            PassBack [_StencilBackPassOp]
            FailBack [_StencilBackFailOp]
            ZFailBack [_StencilBackDepthFailOp]
            
            CompFront [_StencilFrontTestCompare]
            PassFront [_StencilFrontPassOp]
            FailFront [_StencilFrontFailOp]
            ZFailFront [_StencilFrontDepthFailOp]
        }
        ZWrite [_ZWriteEnable]
        ZTest [_ZTestCompare]
        ColorMask [_ColorMask]

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS                    //接受阴影
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE            //产生阴影
        #pragma multi_compile _ _SHADOWS_SOFT                          //软阴影
        ENDHLSL
        
        Pass
        {
            Name "Elysia PBR"
            
            HLSLINCLUDE
            #include_with_pragmas "Assets/Materials/PBR/PBR.hlsl"
            ENDHLSL

            HLSLPROGRAM
            #pragma shader_feature _PCF_LOW _PCF_MIDDLE _PCF_HIGH
            #pragma shader_feature _SHADINGMODEL_UNLIT _SHADINGMODEL_DEFAULTLIT _SHADINGMODEL_DISNEY _ShadingModel_Subsurface _ShadingModel_Hair _ShadingModel_Cloth _ShadingModel_ClearCoat
            #pragma vertex PBRVS
            #pragma fragment PBRPS
            ENDHLSL
        }

        Pass
        {
            Name "Sample Linear 01 Depth For Light Shaft"
            Tags
            {
                "LightMode" = "SampleLinear01Depth"
            }
            
            HLSLINCLUDE
            #include "Assets/Materials/Common.hlsl"
            ENDHLSL
            
            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment SampleLinearDepth

            PSInput VS(VSInput i)
            {
                PSInput o;
                
                o.posCS = mul(UNITY_MATRIX_MVP, float4(i.posOS, 1.f));
                o.posWS = mul(UNITY_MATRIX_M, float4(i.posOS, 1.f));
                
                o.uv = i.uv;
                #if defined (UNITY_UV_STARTS_AT_TOP)
                    o.uv.y = 1 - o.uv.y;
                #endif

                return o;
            }

            PSOutput SampleLinearDepth(PSInput i)
            {
                PSOutput o;

                float viewDepth = length(i.posWS - _WorldSpaceCameraPos) / _ProjectionParams.z;
                o.color.r = viewDepth;
                
                return o;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
