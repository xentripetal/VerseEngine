using Verse.Assets;
using Verse.ECS;
using Verse.MoonWorks.Graphics;

namespace Verse.MoonWorks.Assets;

public struct ImageSettings : ISettings { }

/// <summary>
/// An in-memory texture asset. Will be uploaded to the gpu when used
/// </summary>
public struct Image : IAsset<Image>
{
	public uint Width;
	public uint Height;
	public Memory<byte>? PixelData;
	public TextureFormat Format;
	public TextureUsageFlags Flags;
	public RenderAssetUsage AssetUsage;
	/// <summary>
	/// If not null, a custom sampler will be made for this image when it's uploaded to the GPU. Else it will use
	/// the default sampler.
	/// </summary>
	public SamplerCreateInfo? SamplerCreateInfo;

	public Image(
		Memory<byte> pixelData, uint width, uint height, TextureFormat format, TextureUsageFlags flags = TextureUsageFlags.Sampler,
		RenderAssetUsage assetUsage = RenderAssetUsage.MainWorld | RenderAssetUsage.RenderWorld)
	{
		PixelData = pixelData;
		Width = width;
		Height = height;
		Format = format;
		Flags = flags;
		AssetUsage = assetUsage;
	}
}

public class ImageLoader : IAssetLoader<Image, ImageSettings>, IFromWorld<ImageLoader>
{
	public List<string> Extensions => ["png", "jpg", "jpeg", "bmp"];

	public unsafe Task<Image> Load(Stream stream, ImageSettings settings, LoadContext context)
	{
		return Task.Run(() => {
			var buffer = new byte[stream.Length];
			stream.ReadExactly(buffer);
			var pixelData = ImageUtils.GetPixelDataFromBytes(buffer, out var width, out var height, out var sizeInBytes);
			var pixelSpan = new Memory<byte>(new ReadOnlySpan<byte>(pixelData, (int)sizeInBytes).ToArray());
			ImageUtils.FreePixelData(pixelData);
			return new Image(pixelSpan, width, height, TextureFormat.R8G8B8A8Unorm);
		});
	}
	
	public static ImageLoader FromWorld(World world) => new ImageLoader();
}