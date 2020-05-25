using System;
using UnityEngine.Rendering.LWRP;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public enum RenderQueueType
    {
        Opaque,
        Transparent,
    }
    
    // This ScriptableRendererFeature uses the RenderObjects as a base. However, it uses a main Camera's culling results
    // and uses another disabled Camera's settings to render to a RenderTexture (set on the disabled Camera). This takes
    // for granted that filtered Renderers passes the main Camera's culling and are therefore visible in the main
    // Camera's frustum. Please solely use this as an example and test/profile/modify accordingly before using in
    // production.
    public class RenderObjectsToRT : ScriptableRendererFeature
    {
        [System.Serializable]
        public class RenderObjectsToRTSettings
        {
            public string passTag = "RenderObjectsToRTFeature";
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

            public FilterSettingsRT filterSettings = new FilterSettingsRT();

            public Material overrideMaterial = null;
            public int overrideMaterialPassIndex = 0;

            public bool overrideDepthState = false;
            public CompareFunction depthCompareFunction = CompareFunction.LessEqual;
            public bool enableWrite = true;

            public StencilStateData stencilSettings = new StencilStateData();

            public CustomCameraSettingsRT cameraSettings = new CustomCameraSettingsRT();
        }
        
        [System.Serializable]
        public class FilterSettingsRT
        {
            // TODO: expose opaque, transparent, all ranges as drop down
            public RenderQueueType RenderQueueType;
            public LayerMask LayerMask;
            public string[] PassNames;
            public uint RenderingLayerMask;

            public FilterSettingsRT()
            {
                RenderQueueType = RenderQueueType.Opaque;
                LayerMask = 0;
                RenderingLayerMask = UInt32.MaxValue;
            }
        }

        [System.Serializable]
        public class CustomCameraSettingsRT
        {
            public string cameraTag;
            public Camera camera;
        }

        public RenderObjectsToRTSettings settings = new RenderObjectsToRTSettings();

        RenderObjectsToRTPass renderObjectsPass;

        public override void Create()
        {
            if (settings.cameraSettings.camera == null)
            {
                // NOTE: This API is being used for simplicity's sake. Please modify this to hook into a static
                // Camera manager in your project.
                GameObject cameraGO = GameObject.FindWithTag(settings.cameraSettings.cameraTag);
                if (cameraGO != null)
                {
                    Camera camera = cameraGO.GetComponent<Camera>();
                    settings.cameraSettings.camera = camera;
                }
            }

            FilterSettingsRT filter = settings.filterSettings;
            renderObjectsPass = new RenderObjectsToRTPass(settings.passTag, settings.Event, filter.PassNames,
                filter.RenderQueueType, filter.LayerMask, filter.RenderingLayerMask, settings.cameraSettings);

            renderObjectsPass.overrideMaterial = settings.overrideMaterial;
            renderObjectsPass.overrideMaterialPassIndex = settings.overrideMaterialPassIndex;

            if (settings.overrideDepthState)
                renderObjectsPass.SetDepthState(settings.enableWrite, settings.depthCompareFunction);

            if (settings.stencilSettings.overrideStencilState)
                renderObjectsPass.SetStencilState(settings.stencilSettings.stencilReference,
                    settings.stencilSettings.stencilCompareFunction, settings.stencilSettings.passOperation,
                    settings.stencilSettings.failOperation, settings.stencilSettings.zFailOperation);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(renderObjectsPass);
        }
    }
}

