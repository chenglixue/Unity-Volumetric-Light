using System;
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
            public ComputeShader m_blurShader;
            
            [Header("Light Shaft Settings")]
            [Range(1, 10)]public int downSample = 1;
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
            
            m_LightShaftRenderPass.ConfigureInput(ScriptableRenderPassInput.Motion | ScriptableRenderPassInput.Depth);
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
        private RenderTargetIdentifier  _cameraRTI;
        private RenderTargetIdentifier  _cameraColorRTI;
        private RenderTargetIdentifier  _cameraDepthRTI;
        private RenderTargetIdentifier  _lightShaftRTI;
        private RenderTargetIdentifier  _blurRTI;
        private RenderTargetIdentifier  _lowResDepthRTI;
        internal struct ShaderID
        {
            public static int _cameraColorTexID = Shader.PropertyToID("_CameraColorTexture");
            public static int _lightShaftTexID  = Shader.PropertyToID("_LightShaftTex");
            public static int _blurTexID        = Shader.PropertyToID("_BlurTex");
            public static int _lowResDepthTexID = Shader.PropertyToID("_LowResDepthTex");
        }
        private Vector2Int m_texSize;
        
        private Matrix4x4 _Pre_Matrix_VP;
        private Matrix4x4 _Curr_Matrix_VP;
        #endregion

        #region Setup
        public LightShaftRenderPass(LightShaftRF.PassSetting passSetting)
        {
            this._passSetting = passSetting;
            renderPassEvent = _passSetting.m_passEvent;
            
            _material = new Material(Shader.Find("S_LightShaft"));
            _blurShader = _passSetting.m_blurShader;
        }

        public void Setup(LightShaftVolume lightShaftVolume, UniversalRenderer renderer)
        {
            _lightShaftVolume = lightShaftVolume;
            _Renderer = renderer;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _descriptor = renderingData.cameraData.cameraTargetDescriptor;
            var originDesc = _descriptor;
            _descriptor.msaaSamples = 1;
            m_texSize = new Vector2Int(_descriptor.width, _descriptor.height);

            _cameraRTI      = _Renderer.cameraColorTarget;
            _cameraDepthRTI = _Renderer.cameraDepthTarget;
            
            InitRTI(ref _cameraColorRTI, ShaderID._cameraColorTexID, _descriptor, cmd,
                1, 1, RenderTextureFormat.Default, 0, 
                true, true, false, FilterMode.Point);
            
            InitRTI(ref _lightShaftRTI, ShaderID._lightShaftTexID, _descriptor, cmd,
                (int)_passSetting.downSample, (int)_passSetting.downSample, RenderTextureFormat.Default, 0, 
                true, true, false, FilterMode.Point);
            
            InitRTI(ref _blurRTI, ShaderID._blurTexID, _descriptor, cmd,
                (int)_passSetting.downSample, (int)_passSetting.downSample, RenderTextureFormat.Default, 0, 
                true, true, false, FilterMode.Point);
            
            InitRTI(ref _lowResDepthRTI, ShaderID._lowResDepthTexID, _descriptor, cmd,
                (int)_passSetting.downSample, (int)_passSetting.downSample, RenderTextureFormat.R16, 24, 
                true, true, false, FilterMode.Point);
            
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
                
                var viewMatrix = renderingData.cameraData.GetViewMatrix();
                var projectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
                cmd.SetGlobalMatrix("Matrix_V", viewMatrix);
                cmd.SetGlobalMatrix("Matrix_I_V", viewMatrix.inverse);
                cmd.SetGlobalMatrix("Matrix_P", projectionMatrix);
                cmd.SetGlobalMatrix("Matrix_I_P", projectionMatrix.inverse);
                _Curr_Matrix_VP = projectionMatrix * viewMatrix;
                cmd.SetGlobalMatrix("Matrix_VP", _Curr_Matrix_VP);
                cmd.SetGlobalMatrix("Matrix_I_VP", _Curr_Matrix_VP.inverse);
                cmd.SetGlobalMatrix("_Pre_Matrix_VP", _Pre_Matrix_VP);
            }
        }
        
        void InitRTI(ref RenderTargetIdentifier RTI, int texID, RenderTextureDescriptor descriptor, CommandBuffer cmd,
            int downSampleWidth, int downSampleHeight, RenderTextureFormat colorFormat, 
            int depthBufferBits, bool isUseMipmap, bool isAutoGenerateMips, bool isEnableRandomWrite,
            FilterMode filterMode)
        {
            descriptor.width           /= downSampleWidth;
            descriptor.height          /= downSampleHeight;
            descriptor.colorFormat      = colorFormat;
            descriptor.depthBufferBits  = depthBufferBits;
            descriptor.useMipMap        = isUseMipmap;
            descriptor.autoGenerateMips = isAutoGenerateMips;
            descriptor.enableRandomWrite = true;
            
            RTI = new RenderTargetIdentifier(texID);
            cmd.GetTemporaryRT(texID, descriptor, filterMode);
            cmd.SetGlobalTexture(texID, RTI);
        }
        
        void InitRT(ref RenderTargetIdentifier RTI, ref RenderTexture RT, int RTID, CommandBuffer cmd, Material material,
            RenderTextureDescriptor descriptor, int downSampleWidth, int downSampleHeight, RenderTextureFormat colorFormat,
            int depthBufferBits, bool isUseMipmap, bool isAutoGenerateMips, bool isEnableRandomWrite, FilterMode filterMode)
        {
            descriptor.width            = descriptor.width / downSampleWidth;
            descriptor.height           = descriptor.height / downSampleHeight;
            descriptor.useMipMap        = isUseMipmap;
            descriptor.autoGenerateMips = isAutoGenerateMips;
            descriptor.enableRandomWrite= isEnableRandomWrite;
            descriptor.depthBufferBits  = depthBufferBits;
            descriptor.colorFormat      = colorFormat;
            RT                          = RenderTexture.GetTemporary(descriptor);
            RT.filterMode               = filterMode;
            RTI                         = new RenderTargetIdentifier(RT);
            _material.SetTexture(RTID, RT);
        }
        #endregion

        #region Execute

        Vector4 GetTextureSizeParams(Vector2Int texSize)
        {
            return new Vector4(texSize.x, texSize.y, 1f / texSize.x, 1f / texSize.y);
        }

        void DoVolumetricLight(CommandBuffer cmd, Material material)
        {
            try
            {
                if (material == null) return;
                cmd.Blit(null, _lightShaftRTI, material, 0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
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
                cmd.Blit(_cameraRTI, _cameraColorRTI);
                DoVolumetricLight(cmd, _material);
                
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
                _descriptor.enableRandomWrite = true;
                var tempDesc = _descriptor;
                
                int kawaseRTID = Shader.PropertyToID("_KawaseRT");
                cmd.GetTemporaryRT(kawaseRTID, tempDesc);
                RTIDs.Add(kawaseRTID);
                RTSizes.Add(m_texSize);
                
                float downSampleAmount = Mathf.Log(_passSetting.GetRadius() + 1.0f) / 0.693147181f;
                int downSampleCount = Mathf.FloorToInt(downSampleAmount);
                float offsetRatio = downSampleAmount - (float)downSampleCount;
                
                var lastRTSize = m_texSize;
                int lastRTID = ShaderID._lightShaftTexID;
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
                    DoKawaseLinear(cmd, _lightShaftRTI, RTIDs[0], RTSizes[0], offsetRatio, _blurShader);
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

                cmd.Blit(RTIDs[0], _blurRTI);
                cmd.Blit(_blurRTI, _lowResDepthRTI, _material, 1);
                cmd.Blit(_cameraColorRTI, _cameraRTI, _material, 2);
            }
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(ShaderID._lightShaftTexID);
            cmd.ReleaseTemporaryRT(ShaderID._blurTexID);
            cmd.ReleaseTemporaryRT(ShaderID._lowResDepthTexID);
        }
        #endregion
    }
}


