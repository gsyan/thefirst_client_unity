using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

// URP Radial Blur Render Feature (Render Graph API)
public class RadialBlurFeature : ScriptableRendererFeature
{
    public static RadialBlurFeature Instance { get; private set; }

    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Shader shader;
        [Range(0f, 1f)] public float intensity = 0f;
        [Range(0f, 1f)] public float centerX = 0.5f;
        [Range(0f, 1f)] public float centerY = 0.5f;
        [Range(4, 32)] public int samples = 16;
    }

    public Settings settings = new Settings();
    private RadialBlurPass m_pass;
    private Material m_material;

    public override void Create()
    {
        Instance = this;
        if (settings.shader != null)
            m_material = CoreUtils.CreateEngineMaterial(settings.shader);

        m_pass = new RadialBlurPass(settings, m_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null || settings.intensity <= 0f)
            return;

        if (m_material == null)
        {
            m_material = CoreUtils.CreateEngineMaterial(settings.shader);
            m_pass = new RadialBlurPass(settings, m_material);
        }

        renderer.EnqueuePass(m_pass);
    }

    public void SetIntensity(float value)
    {
        settings.intensity = Mathf.Clamp01(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (m_material != null)
            CoreUtils.Destroy(m_material);
    }

    class RadialBlurPass : ScriptableRenderPass
    {
        private Settings m_settings;
        private Material m_material;

        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int CenterXID = Shader.PropertyToID("_CenterX");
        private static readonly int CenterYID = Shader.PropertyToID("_CenterY");
        private static readonly int SamplesID = Shader.PropertyToID("_Samples");

        public RadialBlurPass(Settings settings, Material material)
        {
            m_settings = settings;
            m_material = material;
            renderPassEvent = settings.renderPassEvent;
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_material == null || m_settings.intensity <= 0f)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var source = resourceData.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_RadialBlurTemp";
            desc.clearBuffer = false;
            var temp = renderGraph.CreateTexture(desc);

            m_material.SetFloat(IntensityID, m_settings.intensity);
            m_material.SetFloat(CenterXID, m_settings.centerX);
            m_material.SetFloat(CenterYID, m_settings.centerY);
            m_material.SetInt(SamplesID, m_settings.samples);

            // Pass 1: source → temp (블러 적용)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RadialBlur", out var passData))
            {
                passData.material = m_material;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: temp → source (복사)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RadialBlur_Copy", out var passData))
            {
                passData.source = temp;

                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }
    }
}
