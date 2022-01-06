using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Experimental.Rendering.Universal
{
    public class RenderRenderers : ScriptableRendererFeature
    {
        [System.Serializable]
        public class RenderRenderersSettings
        {
            public string profilerTag;
            public RenderPassEvent renderPassEvent;
            public CustomCameraSettings cameraSettings = new CustomCameraSettings();
            public RenderererToRenderSettings renderersToRenderSettings = new RenderererToRenderSettings();
        }

        [System.Serializable]
        public class CustomCameraSettings
        {
            public string cameraName;
            public Camera camera;
        }

        [System.Serializable]
        public class RenderererToRenderSettings
        {
            public List<string> rendererNames;
            public List<int> passesToRender;
            public List<List<int>> submeshesToRender;
            public List<Renderer> renderersToRender;
        }
            
        public RenderRenderersSettings settings = new RenderRenderersSettings();

        RenderRenderersPass renderRenderersPass;

        public override void Create()
        {
            renderRenderersPass = new RenderRenderersPass(settings.profilerTag, settings.renderPassEvent, settings.cameraSettings, settings.renderersToRenderSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(renderRenderersPass);
        }
    }
}

