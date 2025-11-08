using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;
using Verse.MoonWorks.Assets;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Assets;

namespace Verse.Render.TextureAsset;

public record struct DefaultSamplerDescriptor(SamplerCreateInfo SamplerCreateInfo);

public struct TexturePlugin : IPlugin
{
	public SamplerCreateInfo DefaultSampler = SamplerCreateInfo.PointClamp;
	public TexturePlugin() { }

	public void Build(App app)
	{
		app.AddPlugin<RenderAssetPlugin<GPUImage, Image, ParamSet<ResMut<ResourceUploader>, Res<DefaultSampler>, ResMut<GraphicsDevice>>>>();
		var renderApp = app.GetSubApp(RenderApp.Name);
		if (renderApp != null) {
			renderApp.InsertResource(new DefaultSamplerDescriptor(DefaultSampler));
			var buildSampler = BuildDefaultSampler;
			renderApp.AddSystems(RenderSchedules.Startup, FuncSystem.Of(buildSampler));
		}
	}

	public static void BuildDefaultSampler(Res<DefaultSamplerDescriptor> descriptor, ResMut<GraphicsDevice> device, World world)
	{
		var sampler = Sampler.Create(device, descriptor.Value.SamplerCreateInfo);
		world.InsertResource(new DefaultSampler(sampler));
	}
}