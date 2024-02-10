using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// https://forum.unity.com/threads/rendertargethandle-is-obsolete-deprecated-in-favor-of-rthandle.1211052/
namespace StartledSeal.Rendering
{
    public class ScreenSpaceOutlines : ScriptableRendererFeature
    {
        #region Additional Render Pass Implement

        [Serializable]
        private class ViewSpaceNormalsTextureSettings
        {
            public RenderTextureFormat colorFormat;
            public int depthBufferBits;
            public FilterMode filterMode;
            public Color backgroundColor;
        }
        
        // Render view space normal texture to _SceneViewSpaceNormals
        private class ViewSpaceNormalsTexturePass : ScriptableRenderPass
        {
            private ViewSpaceNormalsTextureSettings _viewSpaceNormalsTextureSettings;
            private readonly List<ShaderTagId> _shaderTagIdList;
            private readonly Material _normalsMaterial;
            private readonly RTHandle _normals;
            private FilteringSettings _filteringSettings;
            
            public ViewSpaceNormalsTexturePass(RenderPassEvent renderPassEvent,
                ViewSpaceNormalsTextureSettings viewSpaceNormalsTextureSettings,
                Shader normalsShader, LayerMask layerMask)
            {
                this.renderPassEvent = renderPassEvent;
                _viewSpaceNormalsTextureSettings = viewSpaceNormalsTextureSettings;
                _shaderTagIdList = new List<ShaderTagId>{
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("LightWeightForward"),
                    new ShaderTagId("SRPDefaultUnlit")
                };
                _normalsMaterial = new Material(normalsShader);
                _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                
                _normals = RTHandles.Alloc("_SceneViewSpaceNormals", name: "_SceneViewSpaceNormals");
            }

            // Called before render pass
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                RenderTextureDescriptor normalsTextureDescriptor = cameraTextureDescriptor;
                normalsTextureDescriptor.colorFormat = _viewSpaceNormalsTextureSettings.colorFormat;
                normalsTextureDescriptor.depthBufferBits = _viewSpaceNormalsTextureSettings.depthBufferBits;
                
                cmd.GetTemporaryRT(
                    Shader.PropertyToID(_normals.name), 
                    normalsTextureDescriptor, 
                    _viewSpaceNormalsTextureSettings.filterMode);
                
                ConfigureTarget(_normals);
                ConfigureClear(ClearFlag.All, _viewSpaceNormalsTextureSettings.backgroundColor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("SceneViewSpaceNormalsTextureCreation")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    
                    DrawingSettings drawingSettings = CreateDrawingSettings(
                        _shaderTagIdList, ref renderingData,
                        renderingData.cameraData.defaultOpaqueSortFlags);
                    drawingSettings.overrideMaterial = _normalsMaterial;
                    
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // Called after camera finished render
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(_normals.name));
            }
        }
        
        private class ScreenSpaceOutlinePass : ScriptableRenderPass
        {
            public ScreenSpaceOutlinePass(RenderPassEvent renderPassEvent)
            {
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
            }
        }
        
        #endregion

        [SerializeField] private RenderPassEvent _renderPassEvent;
        [SerializeField] private LayerMask _layerMask; // Only outline object I want, ex: charater ..
        
        [SerializeField] private ViewSpaceNormalsTextureSettings _viewSpaceNormalsTextureSettings;
        [SerializeField] private Shader _normalsShader;
        
        private ViewSpaceNormalsTexturePass _viewSpaceNormalsTexturePass;
        private ScreenSpaceOutlinePass _screenSpaceOutlinePass;

        public override void Create()
        {
            _viewSpaceNormalsTexturePass = new ViewSpaceNormalsTexturePass(
                _renderPassEvent, 
                _viewSpaceNormalsTextureSettings, 
                _normalsShader,
                _layerMask);
            _screenSpaceOutlinePass = new ScreenSpaceOutlinePass(_renderPassEvent);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_viewSpaceNormalsTexturePass);
            renderer.EnqueuePass(_screenSpaceOutlinePass);
        }
    }
}