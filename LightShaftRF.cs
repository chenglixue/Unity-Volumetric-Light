using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class LightShaftRF : ScriptableRendererFeature
    {
        [System.Serializable]
        public class PassSetting
        {
            public string m_profilerTag = "LightShaft Pass";
            public RenderPassEvent m_passEvent = RenderPassEvent.AfterRenderingTransparents;
            public Shader m_shader;
            public ComputeShader m_blurShader;
            public RenderTextureFormat m_texFormat;
            
            public enum DownSample
            {
                off = 1,
                half = 2,
                third = 3,
                fourth = 4
            }
            [Header("Light Shaft Settings")]
            public DownSample downSample = DownSample.off;
            [Range(1, 16)]   public int stepCount = 16;
            [Range(0, 1000)] public float maxDistance = 400f;
            [Range(-1, 1)]   public float HGFactor = 1f;
            [Range(0.01f, 1)]    public float TransmittanceFactor = 0f;
            [Range(0, 1200)] public float heightFromSeaLevel = 0f;
                             public Texture2D blueNoiseTex;
                             public Color lightShaftColor = Color.white;
            [Range(0, 2)]    public float brightness = 1f;
            
            [Header("Blur Settings")]
            [Range(0, 1)]    public float blurIntensity = 1f;
            [Range(0, 255)]  public float blurMaxRadius = 32f;
            public float GetRadius()
            {
                return blurIntensity * blurMaxRadius;
            }

            [System.Serializable]
            public class TransparentSetting
            {
                public enum EnableTransparentColor
                {
                    off = 0,
                    on = 1
                }
                public EnableTransparentColor enableTransparentColor = EnableTransparentColor.on;
                
                [Range(1, 16)]public int stepCount = 8;
                [Range(0, 1000)] public float maxDistance = 50f;
                [Range(0, 5)] public float colorIntensity = 1;
            }
            [Header("Transparent Color Settings")]
            public TransparentSetting transparentSetting = new TransparentSetting();
        }
    
        public PassSetting m_setting = new PassSetting();
        LightShaftRenderPass m_LightShaftRenderPass;
    
        public override void Create()
        {
            m_LightShaftRenderPass = new LightShaftRenderPass(m_setting);
        }
    
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var lightShaftVolume = VolumeManager.instance.stack.GetComponent<LightShaftVolume>();
            
                m_LightShaftRenderPass.Setup(lightShaftVolume, (UniversalRenderer)renderer);
                renderer.EnqueuePass(m_LightShaftRenderPass);
        }
    }   
    
    class LightShaftRenderPass : ScriptableRenderPass
    {
        #region Variable
        private LightShaftRF.PassSetting _passSetting;
        private Material                 _material;
        private ComputeShader            _blurShader;
        private LightShaftVolume         _lightShaftVolume;
        private UniversalRenderer        _Renderer;
        
        private RenderTextureDescriptor _descriptor;
        private RenderTargetIdentifier  _cameraColorIden;
        private RenderTargetIdentifier  _cameraDepthRT;
        private RenderTargetIdentifier  _OddBuffer;
        private RenderTargetIdentifier  _EvenBuffer;
        private RenderTargetIdentifier  _LowResDepthRT;
        private static int _OddBufferTexID   = Shader.PropertyToID("_OddBuffer");
        private static int _EvenBufferTexID  = Shader.PropertyToID("_EvenBuffer");
        private static int _cameraColorTexID = Shader.PropertyToID("_CameraColorTexture");
        private static int _cameraDepthTexID = Shader.PropertyToID("_CameraDepthTexture");
        private static int _lowResDepthTexID = Shader.PropertyToID("_LowResDepthTexture");
        private Vector2Int m_texSize;
        #endregion

        #region Setup
        public LightShaftRenderPass(LightShaftRF.PassSetting passSetting)
        {
            this._passSetting = passSetting;
            renderPassEvent = _passSetting.m_passEvent;
            
            if (_passSetting.m_shader == null)
            {
                _material = CoreUtils.CreateEngineMaterial("LightShaft");
            }
            else
            {
                _material = new Material(_passSetting.m_shader);
            }

            _blurShader = _passSetting.m_blurShader;
        }

        public void Setup(LightShaftVolume lightShaftVolume, UniversalRenderer renderer)
        {
            _lightShaftVolume = lightShaftVolume;
            _Renderer = renderer;
            
            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _descriptor = renderingData.cameraData.cameraTargetDescriptor;
            var originDesc = _descriptor;
            _descriptor.msaaSamples = 1;
            _descriptor.enableRandomWrite = true;
            _descriptor.depthBufferBits = 0;
            _descriptor.colorFormat = _passSetting.m_texFormat;
            _descriptor.width  /= (int)_passSetting.downSample;
            _descriptor.height /= (int)_passSetting.downSample;
            m_texSize = new Vector2Int(_descriptor.width, _descriptor.height);

            _cameraColorIden = _Renderer.cameraColorTarget;
            _cameraDepthRT = _Renderer.cameraDepthTarget;
            cmd.SetGlobalTexture(_cameraDepthTexID, _cameraDepthRT);
            cmd.SetGlobalTexture(_cameraColorTexID, _cameraColorIden);
            
            _OddBuffer     = new RenderTargetIdentifier(_OddBufferTexID);
            _EvenBuffer    = new RenderTargetIdentifier(_EvenBufferTexID);
            _LowResDepthRT = new RenderTargetIdentifier(_lowResDepthTexID);
            cmd.GetTemporaryRT(_OddBufferTexID,  originDesc, FilterMode.Point);
            cmd.GetTemporaryRT(_EvenBufferTexID, _descriptor, FilterMode.Point);
            _descriptor.colorFormat = RenderTextureFormat.R16;
            cmd.GetTemporaryRT(_lowResDepthTexID, _descriptor, FilterMode.Point);
            _descriptor.colorFormat = _passSetting.m_texFormat;
            
            if (_material != null)
            {
                _material.SetInt("_StepCount", _passSetting.stepCount);
                _material.SetFloat("_MaxDistance", _passSetting.maxDistance);
                _material.SetFloat("_HGCoff", _passSetting.HGFactor);
                _material.SetFloat("_TransmittanceFactor", _passSetting.TransmittanceFactor);
                _material.SetFloat("_HeightFromSeaLevel", _passSetting.heightFromSeaLevel);
                _material.SetFloat("_Brightness", _passSetting.brightness);
                _material.SetColor("_LightShaftColor", _passSetting.lightShaftColor);
                _material.SetVector("_TexParams", GetTextureSizeParams(m_texSize));
                _material.SetTexture("_BlueNoiseTex", _passSetting.blueNoiseTex);
                
                _material.SetInt("_TransparentStepCounts", _passSetting.transparentSetting.stepCount);
                _material.SetFloat("_TransparentMaxDistance", _passSetting.transparentSetting.maxDistance);
                _material.SetFloat("_TransparentColorIntensity", _passSetting.transparentSetting.colorIntensity);
            }
        }
        #endregion

        #region Execute

        Vector4 GetTextureSizeParams(Vector2Int texSize)
        {
            return new Vector4(texSize.x, texSize.y, 1f / texSize.x, 1f / texSize.y);
        }
        
        private void DoKawaseSample(CommandBuffer cmd, RenderTargetIdentifier sourceid, RenderTargetIdentifier targetid,
                                Vector2Int sourceSize, Vector2Int targetSize,
                                float offset, bool downSample, ComputeShader computeShader)
        {
            if (!computeShader) return;
            string kernelName = downSample ? "DualBlurDownSample" : "DualBlurUpSample";
            int kernelID = computeShader.FindKernel(kernelName);
            computeShader.GetKernelThreadGroupSizes(kernelID, out uint x, out uint y, out uint z);
            cmd.SetComputeTextureParam(computeShader, kernelID, "_SourceTex", sourceid);
            cmd.SetComputeTextureParam(computeShader, kernelID, "_RW_TargetTex", targetid);
            cmd.SetComputeVectorParam(computeShader, "_SourceSize", GetTextureSizeParams(sourceSize));
            cmd.SetComputeVectorParam(computeShader, "_TargetSize", GetTextureSizeParams(targetSize));
            cmd.SetComputeFloatParam(computeShader, "_BlurOffset", offset);
            cmd.DispatchCompute(computeShader, kernelID,
                                Mathf.CeilToInt((float)targetSize.x / x),
                                Mathf.CeilToInt((float)targetSize.y / y),
                                1);
        }

        private void DoKawaseLinear(CommandBuffer cmd, RenderTargetIdentifier sourceid, RenderTargetIdentifier targetid,
            Vector2Int sourceSize, float offset, ComputeShader computeShader)
        {
            if (!computeShader) return;
            string kernelName = "LerpDownUpTex";
            int kernelID = computeShader.FindKernel(kernelName);
            computeShader.GetKernelThreadGroupSizes(kernelID, out uint x, out uint y, out uint z);
            cmd.SetComputeTextureParam(computeShader, kernelID, "_SourceTex", sourceid);
            cmd.SetComputeTextureParam(computeShader, kernelID, "_RW_TargetTex", targetid);
            cmd.SetComputeVectorParam(computeShader, "_SourceSize", GetTextureSizeParams(sourceSize));
            cmd.SetComputeFloatParam(computeShader, "_BlurOffset", offset);
            cmd.DispatchCompute(computeShader, kernelID,
                                Mathf.CeilToInt((float)sourceSize.x / x),
                                Mathf.CeilToInt((float)sourceSize.y / y),
                                1);
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler(_passSetting.m_profilerTag)))
            {
                cmd.Blit(_cameraColorIden, _OddBufferTexID);
                cmd.SetGlobalTexture("_SourceTex", _OddBufferTexID);
                cmd.Blit(_cameraColorIden, _EvenBufferTexID, _material, 0);
                
                if ((int)_passSetting.transparentSetting.enableTransparentColor == 1)
                {
                    _material.EnableKeyword("_TRANSPARENT_COLOR_ON");
                }
                else
                {
                    _material.DisableKeyword("_TRANSPARENT_COLOR_ON");
                }

                List<int> RTIDs = new List<int>();
                List<Vector2Int> RTSizes = new List<Vector2Int>();
                var tempDesc = _descriptor;
                
                int kawaseRTID = Shader.PropertyToID("_KawaseRT");
                cmd.GetTemporaryRT(kawaseRTID, tempDesc);
                RTIDs.Add(kawaseRTID);
                RTSizes.Add(m_texSize);
                
                float downSampleAmount = Mathf.Log(_passSetting.GetRadius() + 1.0f) / 0.693147181f;
                int downSampleCount = Mathf.FloorToInt(downSampleAmount);
                float offsetRatio = downSampleAmount - (float)downSampleCount;
                
                var lastRTSize = m_texSize;
                int lastRTID = _EvenBufferTexID;
                for (int i = 0; i <= downSampleCount; ++i)
                {
                    int currRTID = Shader.PropertyToID("_KawaseRT" + i.ToString());
                    var currRTSize = new Vector2Int((lastRTSize.x + 1) / 2, (lastRTSize.y + 1) / 2);
                    tempDesc.width = currRTSize.x;
                    tempDesc.height = currRTSize.y;
                    cmd.GetTemporaryRT(currRTID, tempDesc);
                
                    RTIDs.Add(currRTID);
                    RTSizes.Add(currRTSize);
                
                    DoKawaseSample(cmd, lastRTID, currRTID, lastRTSize, currRTSize,
                        1f, true, _blurShader);
                
                    lastRTID = currRTID;
                    lastRTSize = currRTSize;
                }
                if(downSampleCount == 0)
                {
                    DoKawaseSample(cmd, RTIDs[1], RTIDs[0], RTSizes[1], RTSizes[0], 1.0f, false, _blurShader);
                    DoKawaseLinear(cmd, _EvenBufferTexID, RTIDs[0], RTSizes[0], offsetRatio, _blurShader);
                }
                else
                {
                    string intermediateRTName = "_KawaseRT" + (downSampleCount + 1).ToString();
                    int intermediateRTID = Shader.PropertyToID(intermediateRTName);
                    Vector2Int intermediateRTSize = RTSizes[downSampleCount];
                    tempDesc.width = intermediateRTSize.x;
                    tempDesc.height = intermediateRTSize.y;
                    cmd.GetTemporaryRT(intermediateRTID, tempDesc);
                    
                    for (int i = downSampleCount+1; i >= 1; i--)
                    {
                        int sourceID = RTIDs[i];
                        Vector2Int sourceSize = RTSizes[i];
                        int targetID = i == (downSampleCount + 1) ? intermediateRTID : RTIDs[i - 1];
                        Vector2Int targetSize = RTSizes[i - 1];
                    
                        DoKawaseSample(cmd, sourceID, targetID, sourceSize, targetSize, 1.0f, false, _blurShader);
                    
                        if (i == (downSampleCount + 1))
                        {
                            DoKawaseLinear(cmd, RTIDs[i - 1], intermediateRTID, targetSize, offsetRatio, _blurShader);
                            int tempID = intermediateRTID;
                            intermediateRTID = RTIDs[i - 1];
                            RTIDs[i - 1] = tempID;
                        }
                        cmd.ReleaseTemporaryRT(sourceID);
                    }
                    cmd.ReleaseTemporaryRT(intermediateRTID);
                }

                cmd.Blit(RTIDs[0], _EvenBufferTexID);
                cmd.SetGlobalTexture("_LightShaftTex", _EvenBufferTexID);
                cmd.Blit(_cameraColorIden, _lowResDepthTexID, _material, 2);
                cmd.SetGlobalTexture("_LowResDepthTex", _lowResDepthTexID);
                cmd.Blit(_OddBufferTexID, _cameraColorIden, _material, 1);
            }
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_OddBufferTexID);
            cmd.ReleaseTemporaryRT(_EvenBufferTexID);
        }
        #endregion
    }
}


