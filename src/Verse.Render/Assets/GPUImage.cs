using FluentResults;
using Verse.Assets;
using Verse.ECS;
using Verse.MoonWorks.Assets;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;

namespace Verse.Render.Assets;

public record struct DefaultSampler(Sampler Sampler);

public struct GPUImage : IRenderAsset<GPUImage, Image, ParamSet<ResMut<ResourceUploader>, Res<DefaultSampler>, ResMut<GraphicsDevice>>>
{
	public Texture Texture;
	public Sampler Sampler;

	public static PrepareAssetResult<GPUImage> PrepareAsset(
		Image asset, AssetId<Image> assetId, ParamSet<ResMut<ResourceUploader>, Res<DefaultSampler>, ResMut<GraphicsDevice>> param, GPUImage previousAsset)
	{
		var (uploader, defaultSampler, device) = param;
		Texture texture;
		if (asset.PixelData.HasValue) {
			// TODO refactor to have image represent the texture region / view properties instead of always assuming its 2d
			texture = uploader.Value.CreateTexture2D<byte>(asset.PixelData.Value.Span, asset.Format, asset.Flags, asset.Width, asset.Height);
		} else {
			texture = Texture.Create2D(device, asset.Width, asset.Height, asset.Format, asset.Flags);
			// TODO copy on resize. Not really sure how this works in bevy.
		}
		var sampler = defaultSampler.Value.Sampler;
		if (asset.SamplerCreateInfo.HasValue) {
			sampler = Sampler.Create(device, assetId.ToString(), asset.SamplerCreateInfo.Value);
		}
		return new PrepareAssetResult<GPUImage>(new GPUImage {
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