using System.Xml.Serialization;

namespace Verse.Assets;

public interface ISettings { }

public class AssetMeta<TProcessor, TProcessorSettings, TLoader, TLoaderSettings, TAsset> : IAssetMeta
	where TProcessor : IAssetProcessor<TProcessor, TProcessorSettings, TLoader, TLoaderSettings, TAsset>
	where TLoader : IAssetLoader<TAsset, TLoaderSettings>
	where TProcessorSettings : ISettings
	where TLoaderSettings : ISettings
	where TAsset : IAsset
{
	public AssetMeta(AssetAction<TLoaderSettings, TProcessorSettings> asset)
	{
		Asset = asset;
		ProcessedInfo = null;
	}
	
	/// <summary>
	/// Information produced by the <see cref="IAssetProcessor{TSelf,TSettings,TLoader,TLoaderSettings,TAsset}"/> after
	/// processing the asset. This will only exist alongside processed versions of assets. You should not manually
	/// set it in your asset source files.
	/// </summary>
	public ProcessedInfo? ProcessedInfo { get; set; }
	
	/// <summary>
	/// How to handle this asset in the asset system.
	/// </summary>
	public AssetAction<TLoaderSettings, TProcessorSettings> Asset;
	
	public ISettings? LoaderSettings {
		get {
			if (Asset.Type == AssetAction<TLoaderSettings, TProcessorSettings>.ActionType.Load) {
				return Asset.LoaderSettings;
			}
			return null;
		}
		set {
			if (Asset.Type == AssetAction<TLoaderSettings, TProcessorSettings>.ActionType.Load) {
				Asset.LoaderSettings = (TLoaderSettings)value!;
			}
		}
	}
}

/// <summary>
/// Configures how an asset source file should be handled by the asset system.
/// </summary>
/// <typeparam name="TLoaderSettings"></typeparam>
/// <typeparam name="TProcessSettings"></typeparam>
public struct AssetAction<TLoaderSettings, TProcessSettings>
	where TLoaderSettings : ISettings
	where TProcessSettings : ISettings
{
	public enum ActionType
	{
		Load,
		Process,
		Ignore
	}

	public ActionType Type;
	public string Name;
	public TLoaderSettings LoaderSettings;
	public TProcessSettings ProcessSettings;
}

/// <summary>
/// Info produced by the <see cref="IAssetProcessor{TSelf,TSettings,TLoader,TLoaderSettings,TAsset}"/> for a given
/// processed asset. This is used to determine if an sset source file (or its dependencies) has changed.
/// </summary>
/// <param name="AssetHash">Hash of the asset bytes and the asset .meta data</param>
/// <param name="FullHash">Hash of the asset bytes, the asset .meta data, and the full hash of every dependency</param>
/// <param name="Dependencies">Information about the process dependencies used to process this asset.</param>
public record struct ProcessedInfo(int AssetHash, int FullHash, List<ProcessDependencyInfo> Dependencies);

/// <summary>
/// Information about a dependency used to process an asset. This is used to determine whether an assets process deps
/// have changed
/// </summary>
public record struct ProcessDependencyInfo(int FullHash, string AssetPath);

public interface IAssetMeta
{
	/// <summary>
	/// Returns the <see cref="IAssetLoader{TAsset,TSettings}"/> settings, if they exist
	/// </summary>
	public ISettings? LoaderSettings { get; set; }

	public ProcessedInfo? ProcessedInfo { get; set; }

}