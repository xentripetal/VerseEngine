namespace Verse.Assets;

public interface IAssetLoader<TAsset, in TSettings>
	where TAsset : IAsset
	where TSettings : ISettings
{
	public Task<TAsset> Load(Stream stream, TSettings settings, LoadContext context);
	public List<string> Extensions { get; }
}

public class LoadContext { }