using System.Collections.Generic;
using UnityEngine.Rendering.LWRP;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    public class RenderObjectsToRTPass : ScriptableRenderPass
    {
        RenderQueueType renderQueueType;
        FilteringSettings m_FilteringSettings;
        RenderObjectsToRT.CustomCameraSettingsRT m_CameraSettings;
        string m_ProfilerTag;

        public Material overrideMaterial { get; set; }
        public int overrideMaterialPassIndex { get; set; }

        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

        RenderTargetHandle m_ColorTextureHandle;
        RenderTargetHandle m_DepthAttachmentTextureHandle;
            
        public void SetDepthState(bool writeEnabled, CompareFunction function = CompareFunction.Less)
        {
            m_RenderStateBlock.mask |= RenderStateMask.Depth;
            m_RenderStateBlock.depthState = new DepthState(writeEnabled, function);
        }

        public void SetStencilState(int reference, CompareFunction compareFunction, StencilOp passOp, StencilOp failOp, StencilOp zFailOp)
        {
            StencilState stencilState = StencilState.defaultValue;
            stencilState.enabled = true;
            stencilState.SetCompareFunction(compareFunction);
            stencilState.SetPassOperation(passOp);
            stencilState.SetFailOperation(failOp);
            stencilState.SetZFailOperation(zFailOp);

            m_RenderStateBlock.mask |= RenderStateMask.Stencil;
            m_RenderStateBlock.stencilReference = reference;
            m_RenderStateBlock.stencilState = stencilState;
        }

        RenderStateBlock m_RenderStateBlock;

        public RenderObjectsToRTPass(string profilerTag,
            RenderPassEvent renderPassEvent,
            string[] shaderTags,
            RenderQueueType renderQueueType,
            int layerMask,
            uint renderLayerMask,
            RenderObjectsToRT.CustomCameraSettingsRT cameraSettings)
        {
            m_ColorTextureHandle.Init("_CameraColorTexture");
            m_DepthAttachmentTextureHandle.Init("_CameraDepthAttachment");
            
            m_ProfilerTag = profilerTag;
            this.renderPassEvent = renderPassEvent;
            this.renderQueueType = renderQueueType;
            this.overrideMaterial = null;
            this.overrideMaterialPassIndex = 0;
            RenderQueueRange renderQueueRange = (renderQueueType == RenderQueueType.Transparent)
                ? RenderQueueRange.transparent
                : RenderQueueRange.opaque;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask, renderLayerMask);

            if (shaderTags != null && shaderTags.Length > 0)
            {
                foreach (var passName in shaderTags)
                    m_ShaderTagIdList.Add(new ShaderTagId(passName));
            }
            else
            {
                m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }

            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_CameraSettings = cameraSettings;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = (renderQueueType == RenderQueueType.Transparent)
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;

            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = overrideMaterial;
            drawingSettings.overrideMaterialPassIndex = overrideMaterialPassIndex;

            Camera originalCamera = renderingData.cameraData.camera;
            Camera rtCamera = m_CameraSettings.camera;

            if (rtCamera != null && rtCamera.targetTexture != null)
            {
                float originalCameraAspect = (float) originalCamera.pixelWidth / (float) originalCamera.pixelHeight;
                float rtCameraAspect = (float) rtCamera.pixelWidth / (float) rtCamera.pixelHeight;
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
                using (new ProfilingSample(cmd, m_ProfilerTag))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    Matrix4x4 rtProjectionMatrix;
                    if (!rtCamera.orthographic)
                    {
                        rtProjectionMatrix = Matrix4x4.Perspective(rtCamera.fieldOfView, rtCameraAspect,
                            rtCamera.nearClipPlane, rtCamera.farClipPlane);
                    }
                    else
                    {
                        float vertical = rtCamera.orthographicSize;
                        float horizontal = vertical * rtCamera.aspect;

                        float left = horizontal * -1;
                        float right = horizontal;
                        float top = vertical;
                        float bottom = vertical * -1;
                        
                        rtProjectionMatrix = Matrix4x4.Ortho(left, right, bottom, top, rtCamera.nearClipPlane, rtCamera.farClipPlane);
                    }

                    cmd.SetRenderTarget(rtCamera.targetTexture);
                    cmd.SetViewProjectionMatrices(rtCamera.worldToCameraMatrix, rtProjectionMatrix);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                    context.ExecuteCommandBuffer(cmd);

                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings,
                    ref m_RenderStateBlock);
                    
                    // restore
                    Matrix4x4 originalProjectionMatrix = Matrix4x4.Perspective(originalCamera.fieldOfView, originalCameraAspect,
                        originalCamera.nearClipPlane, originalCamera.farClipPlane);

                    cmd.Clear();
                    cmd.SetRenderTarget(m_ColorTextureHandle.Identifier(), m_DepthAttachmentTextureHandle.Identifier());
                    cmd.SetViewProjectionMatrices(originalCamera.worldToCameraMatrix, originalCamera.projectionMatrix);
                }
                
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
