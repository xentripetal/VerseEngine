using System.Runtime.Serialization;
using System.Xml.Serialization;
using Serilog;
using Verse.ECS;

namespace Verse.Assets;

public class AssetLoaders
{
	private List<IUntypedAssetLoader> Loaders = new ();
	private Dictionary<Type, List<int>> AssetTypeToLoader = new ();
	private Dictionary<string, List<int>> ExtensionToLoader = new ();
	private Dictionary<string, int> LoaderNameToIndex = new ();

	public void Register<TLoader, TAsset, TSettings>(TLoader loader)
		where TLoader : IAssetLoader<TAsset, TSettings>
		where TAsset : IAsset
		where TSettings : ISettings, new()
	{
		Loaders.Add(loader);
		if (!AssetTypeToLoader.TryGetValue(typeof(TAsset), out var existing)) {
			existing = new List<int>();
			AssetTypeToLoader[typeof(TAsset)] = existing;
		}
		existing.Add(Loaders.Count - 1);

		foreach (var ext in loader.Extensions) {
			if (!ExtensionToLoader.TryGetValue(ext, out var list)) {
				list = new List<int>();
				ExtensionToLoader[ext] = list;
			} else {
				Log.Warning("Duplicate AssetLoader registered for asset type {loaderType} with extension {ext}. " +
				            "Loader must be specified in a .meta file in order load assets of this type with these extensions", typeof(TLoader), ext);
			}
			list.Add(Loaders.Count - 1);
		}

		LoaderNameToIndex[typeof(TLoader).Name] = Loaders.Count - 1;
	}

	public bool TryGetByName(string name, out IUntypedAssetLoader loader)
	{
		loader = null;
		if (!LoaderNameToIndex.TryGetValue(name, out var index)) {
			return false;
		}
		loader = Loaders[index];
		return true;
	}

	public bool TryGetByExtension(string extension, out IUntypedAssetLoader loader)
	{
		if (ExtensionToLoader.TryGetValue(extension, out var indices)) {
			loader = Loaders[indices[0]];
			return true;
		}
		loader = null;
		return false;
	}

	public bool TryFind(string? typeName, Type? assetType, string? extension, AssetPath? path, out IUntypedAssetLoader loader)
	{
		if (typeName != null) {
			return TryGetByName(typeName, out loader);
		}

		string label = null;
		if (path.HasValue) {
			label = path.Value.Label;
		}

		List<int> candidates = null;
		if (assetType != null && label == null) {
			if (AssetTypeToLoader.TryGetValue(assetType, out var indices)) {
				if (indices.Count == 1) {
					loader = Loaders[indices[0]];
					return true;
				}
				candidates = new List<int>(indices);
			}
		}

		if (extension != null) {
			if (ExtensionToLoader.TryGetValue(extension, out var indices)) {
				if (indices.Count == 1) {
					loader = Loaders[indices[0]];
					return true;
				}
				if (candidates == null) {
					candidates = new List<int>(indices);
				} else {
					candidates = candidates.Intersect(indices).ToList();
				}
			}
		}

		if (candidates == null || candidates.Count == 0) {
			loader = null;
			return false;
		}
		if (candidates.Count == 1) {
			loader = Loaders[candidates[0]];
			return true;
		}
		Log.Warning("Multiple loader candidates found for Asset: {asset}, path: {path}, extension: {ext}", assetType, path, extension);
		loader = Loaders[candidates[0]];
		return true;
	}
}

public interface IUntypedAssetLoader
{
	public Task<UntypedLoadedAsset> Load(Stream stream, IAssetMeta assetMeta, LoadContext context);
	public List<string> Extensions { get; }
	public Type AssetType { get; }
	public IAssetMeta DeserializeMeta(Stream stream);
	public IAssetMeta DefaultMeta();
}

public interface IAssetLoader<TAsset, TSettings> : IUntypedAssetLoader
	where TAsset : IAsset
	where TSettings : ISettings, new()
{
	public Task<TAsset> Load(Stream stream, TSettings settings, LoadContext context);
	Type IUntypedAssetLoader.AssetType => typeof(TAsset);
	async Task<UntypedLoadedAsset> IUntypedAssetLoader.Load(Stream stream, IAssetMeta assetMeta, LoadContext context)
	{
		var settings = assetMeta.LoaderSettings;
		if (settings is not TSettings tSettings) {
			throw new InvalidOperationException(
				$"Asset meta loader settings type {settings.GetType()} does not match expected type {typeof(TSettings)} for loader {GetType()}");
		}
		var asset = await Load(stream, tSettings, context);
		return UntypedLoadedAsset.FromLoaded(context.Finish(asset));
	}

	static XmlSerializer serializer = new XmlSerializer(typeof(AssetMeta<TSettings, TAsset>));
	IAssetMeta IUntypedAssetLoader.DeserializeMeta(Stream stream)
	{
		return (AssetMeta<TSettings, TAsset>)serializer.Deserialize(stream)! ?? throw new InvalidOperationException();
	}

	IAssetMeta IUntypedAssetLoader.DefaultMeta()
	{
		return new AssetMeta<TSettings, TAsset>(
			new AssetAction<TSettings>() {
				Type = AssetActionType.Load,
				LoaderSettings = new TSettings(),
			}
		);
	}
}

public class LoadContext
{
	public AssetServer Server;
	public bool ShoudLoadDependencies;
	public bool PopulateHashes;
	public AssetPath Path;
	public HashSet<UntypedAssetId> Dependencies = new ();
	public Dictionary<AssetPath, AssetHash> LoaderDependencies = new ();
	public Dictionary<string, LabeledAsset> LabeledAssets = new ();

	public LoadContext(AssetServer server, AssetPath path, bool shouldLoadDependencies, bool populateHashes)
	{
		Server = server;
		Path = path;
		ShoudLoadDependencies = shouldLoadDependencies;
		PopulateHashes = populateHashes;
	}

	public LoadedAsset<T> Finish<T>(T value) where T : IAsset
	{
		var asset = new LoadedAsset<T>(value);
		asset.Dependencies = this.Dependencies;
		asset.LoaderDependencies = this.LoaderDependencies;
		asset.LabeledAssets = this.LabeledAssets;
		return asset;
	}
}

public struct LabeledAsset
{
	public UntypedLoadedAsset Asset;
	public UntypedHandle Handle;
}

public class LoadedAsset<TAsset> where TAsset : IAsset
{
	public LoadedAsset(TAsset value)
	{
		Value = value;
		Value.VisitDependencies(x => Dependencies.Add(x));
	}
	public TAsset Value;
	public HashSet<UntypedAssetId> Dependencies = new ();
	public Dictionary<AssetPath, AssetHash> LoaderDependencies = new ();
	public Dictionary<string, LabeledAsset> LabeledAssets = new ();

	public bool TryGetLabeledAsset(string label, out UntypedLoadedAsset labeledAsset)
	{
		if (LabeledAssets.TryGetValue(label, out var asset)) {
			labeledAsset = asset.Asset;
			return true;
		}
		labeledAsset = default;
		return false;
	}

	public IEnumerable<string> Labels => LabeledAssets.Keys;
}

/// <summary>
/// A type erased counterpart to <see cref="LoadedAsset{TAsset}"/>. This is used in places where the loaded type is not
/// statically known.
/// </summary>
public class UntypedLoadedAsset
{
	public UntypedLoadedAsset(IAssetContainer value)
	{
		Value = value;
	}
	public static UntypedLoadedAsset FromLoaded<T>(LoadedAsset<T> asset) where T : IAsset
	{
		var untyped = new UntypedLoadedAsset(asset.Value);
		untyped.Dependencies = asset.Dependencies;
		untyped.LoaderDependencies = asset.LoaderDependencies;
		untyped.LabeledAssets = asset.LabeledAssets;
		return untyped;
	}

	public IAssetContainer Value;
	public HashSet<UntypedAssetId> Dependencies = new ();
	public Dictionary<AssetPath, AssetHash> LoaderDependencies = new ();
	public Dictionary<string, LabeledAsset> LabeledAssets = new ();
}

public interface IAssetContainer
{
	/// <summary>
	/// Insert the asset in this container into the worlds Assets.
	/// </summary>
	public void InsertAsset(UntypedAssetId id, World world);
	public Type AssetType { get; }
}