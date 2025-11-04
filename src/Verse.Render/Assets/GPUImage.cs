using FluentResults;
using Verse.Assets;
using Verse.ECS;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;

namespace Verse.Render.Assets;

public record struct DefaultSampler(Sampler Sampler);

public struct GPUImage : IRenderAsset<GPUImage, Image>
{
	public Texture Texture;
	public Sampler Sampler;

	public static Result<GPUImage> PrepareAsset(Image asset, AssetId<Image> assetId, World renderWorld, GPUImage previousAsset)
	{
		var uploader = renderWorld.Resource<ResourceUploader>();
		var defaultSampler = renderWorld.Resource<DefaultSampler>();
		var device = renderWorld.Resource<GraphicsDevice>();
		Texture texture;
		if (asset.PixelData.HasValue) {
			// TODO refactor to have image represent the texture region / view properties instead of always assuming its 2d
			texture = uploader.CreateTexture2D<byte>(asset.PixelData.Value.Span, asset.Format, asset.Flags, asset.Width, asset.Height);
		} else {
			texture = Texture.Create2D(device, asset.Width, asset.Height, asset.Format, asset.Flags);
			// TODO copy on resize. Not really sure how this works in bevy.
		}
		var sampler = defaultSampler.Sampler;
		if (asset.SamplerCreateInfo.HasValue) {
			sampler = Sampler.Create(device, assetId.ToString(), asset.SamplerCreateInfo.Value);
		}
		return Result.Ok(new GPUImage
		{
			Texture = texture,
			Sampler = sampler
		});
	}

	public static ulong ByteLength(Image asset)
	{
		if (asset.PixelData != null) return (ulong)asset.PixelData.Value.Length;
		return 0;
	}
	
	public static RenderAssetUsage GetUsage(Image asset)
	{
		return asset.AssetUsage;
	}
}