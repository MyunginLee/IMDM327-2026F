using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IMDM327.EDMBoids
{
    [RequireComponent(typeof(Camera))]
    public class ScreenAfterimage : MonoBehaviour
    {
        public UniversalRenderPipelineAsset renderPipeline;
        public bool afterimage = true;
        [Range(0.05f, 2f)] public float afterimageSeconds = 0.35f;

        RenderPipelineAsset previousPipeline;
        UniversalRenderPipelineAsset scenePipeline;

        // 1. Use the existing EDM renderer, which includes the feedback pass.
        void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            scenePipeline = Instantiate(renderPipeline);
            QualitySettings.renderPipeline = scenePipeline;
        }

        // 2. Give the previous pipeline back when this camera stops.
        void OnDisable()
        {
            if (QualitySettings.renderPipeline == scenePipeline)
                QualitySettings.renderPipeline = previousPipeline;
            Destroy(scenePipeline);
        }
    }
}
