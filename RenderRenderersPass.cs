using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Experimental.Rendering.Universal
{
    public class RenderRenderersPass : ScriptableRenderPass
    {
        string m_ProfilerTag;
        RenderRenderers.CustomCameraSettings m_CameraSettings;
        RenderRenderers.RenderererToRenderSettings m_RenderersToRender;

        RenderTargetHandle m_ColorTextureHandle;
        RenderTargetHandle m_DepthAttachmentTextureHandle;
        
        public RenderRenderersPass(string profilerTag, RenderPassEvent renderPassEvent, RenderRenderers.CustomCameraSettings cameraSettings, RenderRenderers.RenderererToRenderSettings renderersToRenderSettings)
        {
            this.renderPassEvent = renderPassEvent;

            m_CameraSettings = cameraSettings;
            m_RenderersToRender = renderersToRenderSettings;
            m_ProfilerTag = profilerTag;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CameraSettings.camera == null)
            {
                // NOTE: This API is being used for simplicity's sake. Please modify this to hook into a static
                // Camera manager in your project.
                //GameObject cameraGO = GameObject.FindWithTag(m_CameraSettings.cameraTag);
                GameObject cameraGO = GameObject.Find(m_CameraSettings.cameraName);
                if (cameraGO != null)
                {
                    Camera camera = cameraGO.GetComponent<Camera>();
                    m_CameraSettings.camera = camera;
                }
            }

            if (m_RenderersToRender.renderersToRender == null)
                m_RenderersToRender.renderersToRender = new List<Renderer>();

            m_RenderersToRender.renderersToRender.Clear();
            for (int i = 0; i < m_RenderersToRender.rendererNames.Count; i++)
            {
                GameObject rendererGO = GameObject.Find(m_RenderersToRender.rendererNames[i]);
                if (rendererGO != null)
                {
                    Renderer rendererToAdd = rendererGO.GetComponent<Renderer>();
                    if (rendererToAdd != null)
                    {
                        m_RenderersToRender.renderersToRender.Add(rendererToAdd);
                    }
                }
            }
            
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
                    
                    cmd.Clear();
                    
                    for (int i = 0; i < m_RenderersToRender.renderersToRender.Count; i++)
                    {
                        for (int j = 0; j < m_RenderersToRender.renderersToRender[i].sharedMaterials.Count(); j++)
                        {
                            for (int k = 0; k < m_RenderersToRender.passesToRender.Count; k++)
                            {
                                cmd.DrawRenderer(m_RenderersToRender.renderersToRender[i], m_RenderersToRender.renderersToRender[i].sharedMaterials[j], j, m_RenderersToRender.passesToRender[k]);
                            }
                        }
                    }

                    context.ExecuteCommandBuffer(cmd);

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
