using System.Xml.Serialization;

namespace Verse.Assets;

public interface ISettings { }

public class AssetMeta<TLoaderSettings, TAsset> : IAssetMeta
	where TLoaderSettings : ISettings, new()
	where TAsset : IAsset
{
	public AssetMeta(AssetAction<TLoaderSettings> asset)
	{
		Asset = asset;
		ProcessedInfo = null;
	}
	
	/// <summary>
	/// Information produced by the <see cref="IAssetProcess{TSelf,TSettings,TLoader,TLoaderSettings,TAsset}"/> after
	/// processing the asset. This will only exist alongside processed versions of assets. You should not manually
	/// set it in your asset source files.
	/// </summary>
	public ProcessedInfo? ProcessedInfo { get; set; }
	
	/// <summary>
	/// How to handle this asset in the asset system.
	/// </summary>
	public AssetAction<TLoaderSettings> Asset;
	
	[XmlIgnore]
	public ISettings? LoaderSettings {
		get {
			if (Asset.Type == AssetActionType.Load) {
				return Asset.LoaderSettings;
			}
			return null;
		}
		set {
			if (Asset.Type == AssetActionType.Load) {
				Asset.LoaderSettings = (TLoaderSettings)value!;
			}
		}
	}
}

public class AssetMetaMinimal
{
	public AssetActionMinimal Asset;
}

/// <summary>
/// Counterpart to <see cref="AssetAction{TLoaderSettings,TProcessSettings}"/> for fast serialization.
/// </summary>
public struct AssetActionMinimal
{
	public AssetActionType Type;
	public string Name;
}

public interface IAssetMeta
{
	/// <summary>
	/// Returns the <see cref="IAssetLoader{TAsset,TSettings}"/> settings, if they exist
	/// </summary>
	public ISettings? LoaderSettings { get; set; }

	public ProcessedInfo? ProcessedInfo { get; set; }

}

/// <summary>
/// How to handle an asset source file.
/// </summary>
public enum AssetActionType
{
	Load,
	Process,
	Ignore
}

/// <summary>
/// Configures how an asset source file should be handled by the asset system.
/// </summary>
/// <typeparam name="TLoaderSettings"></typeparam>
/// <typeparam name="TProcessSettings"></typeparam>
public struct AssetAction<TLoaderSettings>
	where TLoaderSettings : ISettings
{
	
	public AssetActionType Type;
	public string Name;
	public TLoaderSettings LoaderSettings;

	public AssetActionMinimal Minimal() => new AssetActionMinimal {
		Type = Type,
		Name = Name,
	};
}

/// <summary>
/// Info produced by the <see cref="IAssetProcess{TSelf,TSettings,TLoader,TLoaderSettings,TAsset}"/> for a given
/// processed asset. This is used to determine if an sset source file (or its dependencies) has changed.
/// </summary>
/// <param name="AssetHash">Hash of the asset bytes and the asset .meta data</param>
/// <param name="FullHash">Hash of the asset bytes, the asset .meta data, and the full hash of every dependency</param>
/// <param name="Dependencies">Information about the process dependencies used to process this asset.</param>
public record struct ProcessedInfo(AssetHash AssetHash, AssetHash FullHash, List<ProcessDependencyInfo> Dependencies);

/// <summary>
/// Information about a dependency used to process an asset. This is used to determine whether an assets process deps
/// have changed
/// </summary>
public record struct ProcessDependencyInfo(int FullHash, string AssetPath);

