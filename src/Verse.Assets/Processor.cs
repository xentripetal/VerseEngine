namespace Verse.Assets;

public interface IAssetProcessor<TSelf, TSettings, TLoader, TLoaderSettings, TAsset>
	where TSelf : IAssetProcessor<TSelf, TSettings, TLoader, TLoaderSettings, TAsset>
	where TLoader : IAssetLoader<TAsset, TLoaderSettings>
	where TSettings : ISettings
	where TLoaderSettings : ISettings
	where TAsset : IAsset
{
	/// <summary>
	/// Processes the asset stored on <paramref name="context"/> in some way using the settings stored on <paramref name="meta"/>.
	/// The results are written to <paramref name="writer"/>. The final written procssed asset is loadable using
	/// <typeparamref name="TLoader"/>. This load will use the returned settings.
	/// </summary>
	/// <param name="context">Information about an asset</param>
	/// <param name="meta"></param>
	/// <param name="writer"></param>
	/// <returns></returns>
	public Task<TLoaderSettings> Process(ProcessContext context, AssetMeta<TSelf, TSettings, TLoader, TLoaderSettings, TAsset> meta, StreamWriter writer);
}

public class ProcessContext { }