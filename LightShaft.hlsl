#pragma once

#include "Assets/Materials/Common.hlsl"
#include "Assets/Materials/Math.hlsl"
#include "Assets/Materials/Random.hlsl"

CBUFFER_START(UnityPerMaterial)
half4 _MainTex_TexelSize;
half4 _TAATexSize;

CBUFFER_END
float4 _TexParams;

float3 _SunDirection;
float _HeightFromSeaLevel;
float _TransmittanceFactor;   
float _HGCoff;   
int _StepCount;            
float _MaxDistance;
float _Brightness;
float4 _LightShaftColor;

int _TransparentStepCounts;
float _TransparentMaxDistance;
float _TransparentColorIntensity;

float _TAAScale;
float _TAAWeight;
float2 _Jitter;

float4x4 Matrix_VP;
float4x4 Matrix_I_VP;
float4x4 _Pre_Matrix_VP;

Texture2D<float3> _BlueNoiseTex;
Texture2D<float4> _CameraColorTexture;
Texture2D<float>  _CameraDepthTexture;
Texture2D<float2> _MotionVectorTexture;
Texture2D<float4> _LightShaftTex;
Texture2D<float4> _TAACurrTex;
Texture2D<float4> _TAAPreTex;
Texture2D<float4> _BlurTex;
Texture2D<float>  _LowResDepthTex;
Texture2D<float>  _LinearDepthTex;

struct VSInput
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct PSInput
{
    float2 uv : TEXCOORD0;
    
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD3;
};

struct PSOutput
{
    float4 color : SV_TARGET;
};

float3 ReConstructPosWS(float2 posVP)
{
    #if defined(UNITY_REVERSED_Z)
        half depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, Smp_ClampU_ClampV_Linear, posVP);
    #else
        half depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
    #endif
    
    float3 posNDC = float3(posVP * 2.f - 1.f, depth);
    #if defined(UNITY_UV_STARTS_AT_TOP)
        posNDC.y = -posNDC.y;
    #endif

    float4 posWS = mul(UNITY_MATRIX_I_VP, float4(posNDC, 1.f));
    posWS.xyz /= posWS.w;

    return posWS;
}
float2 TransformWorldToScreen(float3 positionWS)
{
    positionWS = (positionWS - _WorldSpaceCameraPos) * (_ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y)) + _WorldSpaceCameraPos;
    real2 uv = 0;
    real3 toCam = mul(unity_WorldToCamera, positionWS);
    real camPosZ = toCam.z;
    real height = 2 * camPosZ / unity_CameraProjection._m11;    // unity_CameraProjection._m11: tan(y / z)
    real width = _TexParams.x / _TexParams.y * height;
    // 映射[-x, x]到[0, 1]
    uv.x = (toCam.x + width / 2) / width;
    uv.y = (toCam.y + height / 2) / height;

    return uv;
}

float GetShadow(float3 positionWS)
{
    float4 shadowUV = TransformWorldToShadowCoord(positionWS);
    float shadow = MainLightRealtimeShadow(shadowUV);
    
    return shadow;
}

/// -----------------
/// 沿视线方向散射的量(相位函数)
/// -----------------
float GetPhase(float cosTheta)
{
    float a = 1.f - Pow2(_HGCoff);
    float b = 4.f * PI * pow(1.f + Pow2(_HGCoff) - 2.f * _HGCoff * cosTheta, 1.5f);

    return a / b;
}
/// -----------------
/// 大气密度比例函数
/// -----------------
float GetRho()
{
    return exp(-_HeightFromSeaLevel / 1200.f);
}
/// -----------------
/// 最终的散射比例
/// -----------------
float GetScatter(float cosTheta)
{
    return GetPhase(cosTheta) * GetRho();
}
/// -----------------
/// 透光率
/// -----------------
float GetTransmittance(float distance)
{
    return exp(-distance * _TransmittanceFactor * GetRho());
}

inline float4 GetPositionNDC(float2 uv, float rawDepth)
{
    return float4(uv * 2 - 1, rawDepth, 1.f);
}

inline float4 TransformNDCToWS(float4 positionNDC, float4x4 Matrix_I_VP)
{
    float4 positionWS = mul(Matrix_I_VP, positionNDC);
    positionWS /= positionWS.w;
    #if defined (UNITY_UV_STARTS_AT_TOP)
    positionWS.y *= -1;
    #endif

    return positionWS;
}

inline float2 GetCameraMotionVector(float rawDepth, float2 uv,
    float4x4 Matrix_I_VP, float4x4 _Pre_Matrix_VP, float4x4 Matrix_VP)
{
    float4 positionNDC = GetPositionNDC(uv, rawDepth);
    float4 positionWS  = TransformNDCToWS(positionNDC, Matrix_I_VP);

    float4 currPosCS = mul(Matrix_VP, positionWS);
    float4 prePosCS  = mul(_Pre_Matrix_VP, positionWS);

    float2 currPositionSS = currPosCS.xy / currPosCS.w;
    currPositionSS = (currPositionSS + 1) * 0.5f;
    float2 prePositionSS  = prePosCS.xy / prePosCS.w;
    prePositionSS  = (prePositionSS + 1) * 0.5f;

    return currPositionSS - prePositionSS;
}

half3 GetLightShaft(float3 viewOrigin, half3 viewDir, float maxDistance, float2 screenPos)
{
    float3 totalLight = 0.f;
    float3 totalDistance = 0.f;
    
    float2 ditherPos = fmod(floor(screenPos.xy), 4.f);
    float3 ditherDir = _BlueNoiseTex.Sample(Smp_ClampU_ClampV_Linear, ditherPos / 4.f + float2(0.5 / 4.f, 0.5f / 4.f), float2(0, 0));
    
    float stepLength = maxDistance / _StepCount;              // 步长
    float3 step      = stepLength * viewDir;
    float3 currPos   = viewOrigin + viewDir * ditherDir * step;

    #if defined(_TRANSPARENT_COLOR_ON)
        float3 depthRayDir = -_SunDirection;
        float depthStepLength = _TransparentMaxDistance / _TransparentStepCounts;
        float3 depthStep = depthStepLength * depthRayDir;
    #endif

    float scatterFun = GetScatter(dot(viewDir, -_SunDirection));

    UNITY_LOOP
    for(int i = 0; i < _StepCount; ++i)
    {
        float shadow = GetShadow(currPos);
        
        if(shadow > 0.f)
        {
            float3 currColor = _Brightness * shadow * scatterFun * GetTransmittance(totalDistance);

            #if defined(_TRANSPARENT_COLOR_ON)
            float3 depthCurrPos = currPos + depthRayDir * ditherDir;
            UNITY_LOOP
            for(int j = 0; j < _TransparentStepCounts; ++j)
            {
                float2 depth_uv = TransformWorldToScreen(depthCurrPos);
                float distanceCameraToDepth = length(depthCurrPos - _WorldSpaceCameraPos);
                
                if(depth_uv.x < 0.f || depth_uv.y < 0.f || depth_uv.x > 1.f || depth_uv.y > 1.f)
                {
                    break;
                }
                
                float transparentDepth = _LinearDepthTex.Sample(Smp_ClampU_ClampV_Linear, depth_uv).r * _ProjectionParams.z;  // length posws to world camera pos

                // 步进depth点位于半透明物体后面
                if(transparentDepth < distanceCameraToDepth)
                {
                    float4 sourceColor = _SourceTex.Sample(Smp_ClampU_ClampV_Linear, depth_uv) * _TransparentColorIntensity;
                    currColor *= sourceColor;
                }

                depthCurrPos += depthStep;
            }
            #endif

            totalLight += saturate(currColor);
        }
        
        currPos += step;
        totalDistance += stepLength;
    }
    
    half3 result = totalLight * _MainLightColor * _LightShaftColor.rgb * _LightShaftColor.aaa;
    
    return result;
}