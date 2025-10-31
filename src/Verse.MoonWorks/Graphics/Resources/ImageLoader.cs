using Verse.Assets;
using Verse.ECS;

namespace Verse.MoonWorks.Graphics.Resources;

public struct ImageSettings : ISettings { }

/// <summary>
/// An in-memory texture asset. Will be uploaded to the gpu when used
/// </summary>
public struct TestTexture : IAsset<TestTexture>
{
	public uint Width;
	public uint Height;
	public Memory<byte> PixelData;
	public TextureFormat Format;
	public TextureUsageFlags Flags;

	public void Upload(ResourceUploader loader) { }
}

public class ImageLoader : IAssetLoader<TestTexture, ImageSettings>, IFromWorld<ImageLoader>
{
	public List<string> Extensions => ["png", "jpg", "jpeg", "bmp"];

	public unsafe Task<TestTexture> Load(Stream stream, ImageSettings settings, LoadContext context)
	{
		return Task.Run(() => {
			var buffer = new byte[stream.Length];
			stream.ReadExactly(buffer);
			var pixelData = ImageUtils.GetPixelDataFromBytes(buffer, out var width, out var height, out var sizeInBytes);
			var pixelSpan = new Memory<byte>(new ReadOnlySpan<byte>(pixelData, (int)sizeInBytes).ToArray());
			ImageUtils.FreePixelData(pixelData);
			return new TestTexture() {
				Width = width,
				Height = height,
				PixelData = pixelSpan,
				Format = TextureFormat.R8G8B8A8Unorm,
				Flags = TextureUsageFlags.Sampler,
			};
		});
	}
	public static ImageLoader FromWorld(World world) => new ImageLoader();
}