Shader "S_LightShaft"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5
        #pragma enable_d3d11_debug_symbols
        #include "LightShaft.hlsl"

        PSInput VS(VSInput i)
        {
            PSInput o;
            
            o.positionCS = mul(UNITY_MATRIX_MVP, float4(i.positionOS, 1.f));
            o.positionWS = mul(UNITY_MATRIX_M, float4(i.positionOS, 1.f));
            
            o.uv = i.uv;
            #if defined (UNITY_UV_STARTS_AT_TOP)
            if (_TexParams.y < 0.h)
            {
                o.uv.y = 1 - o.uv.y;
            }
            #endif

            return o;
        }
        ENDHLSL
        
        // 0  
        Pass
        {
            Name "Light Shaft Pass"
            
            HLSLPROGRAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS                    //接受阴影
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE            //产生阴影
            #pragma multi_compile _ _SHADOWS_SOFT                          //软阴影
            #pragma shader_feature_local _ _TRANSPARENT_COLOR_ON
            
            #pragma vertex VS
            #pragma fragment LightShaft

            void LightShaft(PSInput i, out PSOutput o)
            {
                float2 channel = floor(i.positionCS);
                // 棋盘格刷新
                clip(channel.y%2 * channel.x%2 + (channel.y+1)%2 * (channel.x+1)%2 - 0.1f);
                
                float3 rePosWS = ReConstructPosWS(i.uv);

                // ray
                half3 viewDir = SafeNormalize(rePosWS - _WorldSpaceCameraPos);
                float3 viewOrigin = _WorldSpaceCameraPos;
                float totalDistance = length(rePosWS - _WorldSpaceCameraPos);
                if(totalDistance > _MaxDistance)
                {
                    rePosWS = viewOrigin + viewDir * _MaxDistance;
                    totalDistance = _MaxDistance;
                }
                
                half3 lightShaft = GetLightShaft(viewOrigin, viewDir, totalDistance, i.positionCS.xy);
                o.color.rgb = lightShaft;
            }
            ENDHLSL
        }

        // 1
        Pass
        {
            Name "Copy Depth"
            
            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment CopyDepth

            PSOutput CopyDepth(PSInput i)
            {
                PSOutput o;
                
                o.color = Linear01Depth(_CameraDepthTexture.Sample(Smp_ClampU_ClampV_Linear, i.uv), _ZBufferParams);
                
                return o;
            }
            ENDHLSL
        }

        // 2
        Pass
        {
            NAME "Composite"
            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment Composite

            PSOutput Composite(PSInput i) : SV_TARGET
            {
                PSOutput o;
                half4 result = 0.h;
                
                float highResDepth = _CameraDepthTexture.Sample(Smp_ClampU_ClampV_Linear, i.uv).r;
                highResDepth = Linear01Depth(highResDepth, _ZBufferParams);
                float lowResDepth1 = _LowResDepthTex.Sample(Smp_ClampU_ClampV_Linear, i.uv, int2(0, 0.5f)).r;
                float lowResDepth2 = _LowResDepthTex.Sample(Smp_ClampU_ClampV_Linear, i.uv, int2(0, -0.5f)).r;
                float lowResDepth3 = _LowResDepthTex.Sample(Smp_ClampU_ClampV_Linear, i.uv, int2(0.5f, 0)).r;
                float lowResDepth4 = _LowResDepthTex.Sample(Smp_ClampU_ClampV_Linear, i.uv, int2(-0.5f, 0)).r;

                float depthDiff1 = abs(highResDepth - lowResDepth1);
                float depthDiff2 = abs(highResDepth - lowResDepth2);
                float depthDiff3 = abs(highResDepth - lowResDepth3);
                float depthDiff4 = abs(highResDepth - lowResDepth4);

                float depthDiffMin = min(min(depthDiff1, depthDiff2), min(depthDiff3, depthDiff4));
                int index = -1;
                if(depthDiffMin == depthDiff1) index = 0;
                else if(depthDiffMin == depthDiff2) index = 1;
                else if(depthDiffMin == depthDiff3) index = 2;
                else if(depthDiffMin == depthDiff4) index = 3;
            
                switch(index)
                {
                    case 0:
                        result += _BlurTex.Sample(Smp_ClampU_ClampV_Point, i.uv, int2(0, 0.5f));
                        break;
                    case 1:
                        result += _BlurTex.Sample(Smp_ClampU_ClampV_Point, i.uv, int2(0, -0.5f));
                        break;
                    case 2:
                        result += _BlurTex.Sample(Smp_ClampU_ClampV_Point, i.uv, int2(0.5f, 0));
                        break;
                    case 3:
                        result += _BlurTex.Sample(Smp_ClampU_ClampV_Point, i.uv, int2(-0.5f, 0));
                        break;
                    default:
                        result += _BlurTex.Sample(Smp_ClampU_ClampV_Point, i.uv);
                        break;
                }
                
                half4 sourceTex = SAMPLE_TEXTURE2D(_CameraColorTexture, Smp_ClampU_ClampV_Linear, i.uv);
                result += sourceTex;
                o.color = result;
                
                return o;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
