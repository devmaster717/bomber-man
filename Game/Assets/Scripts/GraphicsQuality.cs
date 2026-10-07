using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// High or Low graphics (Settings > Graphics). Low turns off shadows, bloom and anti-aliasing, renders at 75%
/// resolution and halves the flames, for phones that struggle. On first launch the game picks one to suit the device.
/// </summary>
public static class GraphicsQuality
{
    public static bool High { get; private set; } = true;

    /// <summary>A guess at what the phone can handle: enough memory, cores and graphics memory for High.</summary>
    public static bool SuitsDevice =>
        SystemInfo.systemMemorySize >= 3000 && SystemInfo.processorCount >= 4 && SystemInfo.graphicsMemorySize >= 512;

    public static void Apply(bool high)
    {
        High = high;
        // The pipeline asset is shared: in the editor, changing it would change the project, so only on the device.
        if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = high ? 1f : 0.75f;
            urp.msaaSampleCount = high ? 2 : 1;
            urp.shadowDistance = high ? 30f : 0f;
        }
        // Bloom and colour grading on the camera that is drawing now (new views read High when they start).
        if (Camera.main != null) Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = high;
    }
}
