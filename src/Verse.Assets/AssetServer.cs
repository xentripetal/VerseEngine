using System.Threading.Channels;
using System.Xml.Serialization;
using CommunityToolkit.HighPerformance;
using Serilog;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Assets;

public class AssetServer
{
	private ReaderWriterLockSlim infoLock = new ();
	private AssetInfos infos;
	private ReaderWriterLockSlim loadersLock = new ();
	private AssetLoaders loaders;
	private AssetSources sources;

	private ChannelWriter<InternalAssetEvent> eventWriter;
	private ChannelReader<InternalAssetEvent> eventReader;
	private XmlSerializer minimalMetaSerializer;

	public AssetServer(AssetSources sources, bool watchingForChanges)
	{
		// Based on bevy's new_with_meta_check but takes out AssetServerMode (nothing is using a processor server yet)
		// and takes out metaCheck and unapproved mode.
		loaders = new AssetLoaders();
		this.sources = sources;
		infos = new AssetInfos();
		infos.WatchingForChanges = true;
		var channel = Channel.CreateUnbounded<InternalAssetEvent>();
		eventReader = channel.Reader;
		eventWriter = channel.Writer;

		minimalMetaSerializer = new XmlSerializer(typeof(AssetMetaMinimal));
	}

	public void RegisterAsset<T>(Assets<T> assets) where T : IAsset
	{
		var handleProvider = assets.GetHandleProvider();
		infoLock.EnterWriteLock();
		try {
			infos.HandleProviders.Add(handleProvider.Type, handleProvider);
			infos.OnDependencyloaded[typeof(T)] = (world, id) => {
				//     world
				//         .resource_mut::<Messages<AssetEvent<A>>>()
				//         .write(AssetEvent::LoadedWithDependencies { id: id.typed() });
			};

			// TODO proxy events to world
			infos.OnDependencyFailed[typeof(T)] = (world, id, path, error) => {
				//     world
				//         .resource_mut::<Messages<AssetLoadFailedEvent<A>>>()
				//         .write(AssetLoadFailedEvent {
				//             id: id.typed(),
				//             path,
				//             error,
				//         });
			};
		}
		finally {
			infoLock.ExitWriteLock();
		}
	}

	public void RegisterLoader<TLoader, TAsset, TLoaderSettings>(TLoader loader)
		where TLoader : IAssetLoader<TAsset, TLoaderSettings>
		where TAsset : IAsset
		where TLoaderSettings : ISettings, new()
	{
		loadersLock.EnterWriteLock();
		try {
			loaders.Register<TLoader, TAsset, TLoaderSettings>(loader);
		}
		finally {
			loadersLock.ExitWriteLock();
		}
	}

	public void RegisterHandleProvider(AssetHandleProvider provider)
	{
		infoLock.EnterWriteLock();
		try {
			infos.HandleProviders.Add(provider.Type, provider);
		}
		finally {
			infoLock.ExitWriteLock();
		}
	}

	public Handle<TAsset> Load<TAsset, TIntoPath>(TIntoPath path)
		where TAsset : IAsset
		where TIntoPath : IIntoAssetPath
	{
		return LoadWithMetaTransform<TAsset, TIntoPath>(path, null, null);
	}

	public Handle<TAsset> Load<TAsset>(AssetPath path)
		where TAsset : IAsset
	{
		return LoadWithMetaTransform<TAsset, AssetPath>(path, null, null);
	}

	public Handle<TAsset> Load<TAsset>(string path)
		where TAsset : IAsset
	{
		return LoadWithMetaTransform<TAsset, AssetPath>(AssetPath.ParseUri(path), null, null);
	}


	public Handle<TAsset> LoadWithMetaTransform<TAsset, TIntoPath>(TIntoPath path, Action<IAssetMeta>? metaTransform, Action? onLoad)
		where TAsset : IAsset
		where TIntoPath : IIntoAssetPath
	{
		infoLock.EnterWriteLock();
		try {
			var actualPath = path.IntoAssetPath();
			var (handle, shouldLoad) = infos.GetOrCreatePathHandle<TAsset>(actualPath, HandleLoadingMode.Request, metaTransform);
			if (shouldLoad) {
				SpawnLoadTask(handle.Untyped(), actualPath, onLoad);
			}
			return handle;
		}
		finally {
			infoLock.ExitWriteLock();
		}
	}

	private void SpawnLoadTask(UntypedHandle handle, AssetPath path, Action? onLoad)
	{
		var task = Task.Run(async () => {
			var res = await LoadInternal(handle, path, false, null);
			onLoad?.Invoke();
			return res;
		});
		infos.PendingTasks.Add(handle.Id(), task);
	}

	private void SendAssetEvent(InternalAssetEvent @event)
	{
		if (!eventWriter.TryWrite(@event)) {
			throw new InvalidOperationException("Failed to write asset event");
		}
	}

	/// <summary>
	/// Performs an async asset load
	/// </summary>
	/// <param name="handle">Must be not null if should_load was true when retrieving the handle.</param>
	/// <param name="path"></param>
	/// <param name="force"></param>
	/// <param name="metaTransform"></param>
	/// <returns>The handle of the asset if one was retrieved</returns>
	private async Task<UntypedHandle?> LoadInternal(UntypedHandle? handle, AssetPath path, bool force, Action<IAssetMeta>? metaTransform)
	{
		Type? inputHandleType = null;
		if (handle.HasValue) {
			inputHandleType = handle.Value.Type;
		}

		IAssetMeta meta;
		IUntypedAssetLoader loader;
		Stream data;
		try {
			(meta, loader, data) = await GetMetaLoaderAndReader(path, inputHandleType);
		}
		catch (AssetLoadException e) {
			Log.Error(e, "Failed loading asset at {path}", path);
			SendAssetEvent(InternalAssetEvent.Failed(handle?.Id() ?? default, e, path));
			throw;
		}
		catch (Exception e) {
			Log.Error(e, "Failed loading asset at {path}", path);
			var ale = new AssetLoadException("Failed loading asset", e);
			SendAssetEvent(InternalAssetEvent.Failed(handle?.Id() ?? default, ale, path));
			throw ale;
		}
		metaTransform?.Invoke(meta);

		UntypedAssetId? assetId;
		UntypedHandle? fetchedHandle;
		bool shouldLoad = false;
		if (handle.HasValue) {
			assetId = handle.Value.Id();
			fetchedHandle = null;
			shouldLoad = true;
		} else {
			infoLock.EnterWriteLock();
			try {
				(fetchedHandle, shouldLoad) =
					infos.GetOrCreatePathHandleInternal(path, path.Label == null ? loader.AssetType : null, HandleLoadingMode.Request, metaTransform);
				assetId = fetchedHandle.Value.Id();
			}
			catch (Exception e) {
				// We couldn't figure out the correct handle. Just return null and tell it to load
				assetId = null;
				fetchedHandle = null;
				shouldLoad = true;
			}
			finally {
				infoLock.ExitWriteLock();
			}
		}

		// verify types match
		if (path.Label == null && assetId.HasValue && assetId.Value.Type != loader.AssetType) {
			throw new AssetLoadException($"Asset {path} has type {assetId.Value.Type} but loader expects {loader.AssetType}");
		}

		if (!shouldLoad && !force) {
			return fetchedHandle;
		}
		UntypedAssetId baseAssetId;
		UntypedHandle? baseHandle;
		AssetPath basePath;
		// If we're a sub-asset, we need to keep the handle alive
		if (path.Label != null) {
			infoLock.EnterWriteLock();
			try {
				basePath = path.WithoutLabel();
				baseHandle = infos.GetOrCreatePathHandleUntyped(basePath, loader.AssetType, HandleLoadingMode.Force, null).Handle;
				baseAssetId = baseHandle.Value.Id();
			}
			finally {
				infoLock.ExitWriteLock();
			}
		} else {
			baseAssetId = assetId!.Value;
			baseHandle = null;
			basePath = path;
		}
		UntypedLoadedAsset asset;
		try {
			asset = await LoadWithMetaLoaderAndReader(basePath, meta, loader, data, true, false);
		}
		catch (AssetLoadException e) {
			SendAssetEvent(InternalAssetEvent.Failed(baseAssetId, e, path));
			throw;
		}

		UntypedHandle? finalHandle;
		if (path.Label != null) {
			if (asset.LabeledAssets.TryGetValue(path.Label!, out var labeledAsset)) {
				finalHandle = labeledAsset.Handle;
			} else {
				throw new AssetLoadException($"Asset {path} does not have a labeled asset with label {path.Label}");
			}
		} else {
			finalHandle = fetchedHandle;
		}
		SendLoadedAssetEvent(baseAssetId, asset);
		return finalHandle;
	}

	private void SendLoadedAssetEvent(UntypedAssetId id, UntypedLoadedAsset loadedAsset)
	{
		foreach (var labeledAsset in loadedAsset.LabeledAssets.Values) {
			SendLoadedAssetEvent(labeledAsset.Handle.Id(), labeledAsset.Asset);
		}
		// bevy drains the labeled assets here. not sure why. Shouldn't be any loop risks
		loadedAsset.LabeledAssets.Clear();
		SendAssetEvent(InternalAssetEvent.Loaded(id, loadedAsset));
	}

	private async Task<UntypedLoadedAsset> LoadWithMetaLoaderAndReader(
		AssetPath path, IAssetMeta meta, IUntypedAssetLoader loader, Stream data, bool loadDeps, bool populateHashes)
	{
		var ctx = new LoadContext(this, path, loadDeps, populateHashes);
		try {
			return await loader.Load(data, meta, ctx);
		}
		catch (Exception e) {
			throw new AssetLoadException("Failed to load asset", e);
		}
	}



	private async Task<(IAssetMeta meta, IUntypedAssetLoader loader, Stream stream)> GetMetaLoaderAndReader(AssetPath path, Type? assetType)
	{
		var source = sources.GetSource(path.Source);
		try {
			var stream = await source.Read(path.Path);
			try {
				var data = await source.ReadMeta(path.Path);
				var rawMeta = minimalMetaSerializer.Deserialize(data);
				if (rawMeta is not AssetMetaMinimal minimalMeta) {
					throw new AssetLoadException("Failed to deserialize minimal asset meta");
				}
				if (minimalMeta.Asset.Type == AssetActionType.Ignore) {
					throw new AssetLoadException($"Asset {path.Path} is marked as ignored");
				}
				if (minimalMeta.Asset.Type == AssetActionType.Process) {
					throw new AssetLoadException($"Asset {path.Path} is marked as processed");
				}
				if (!TryGetAssetLoaderWithTypeName(minimalMeta.Asset.Name, out var loader)) {
					throw new AssetLoadException($"Asset {path.Path} has unknown loader type {minimalMeta.Asset.Name}");
				}
				IAssetMeta meta;
				try {
					meta = loader.DeserializeMeta(data);
				}
				catch (Exception e) {
					throw new AssetLoadException("failed to deserialize asset meta", e);
				}
				return (meta, loader, stream);
			}
			catch (FileNotFoundException) {
				loadersLock.EnterReadLock();
				try {
					if (!loaders.TryFind(null, assetType, null, path, out var loader)) {
						throw new AssetLoadException($"Could not determine loader type for asset {path}");
					}
					return (loader.DefaultMeta(), loader, stream);
				}
				finally { loadersLock.ExitReadLock(); }
			}
		}
		catch (FileNotFoundException e) {
			throw new AssetLoadException($"Asset file not found at {path}", e);
		}
	}

	public bool TryGetAssetLoaderWithTypeName(string typeName, out IUntypedAssetLoader loader)
	{
		loadersLock.EnterReadLock();
		try {
			return loaders.TryGetByName(typeName, out loader);
		}
		finally {
			loadersLock.ExitReadLock();
		}
	}

	public static implicit operator AssetServer(App app) => app.World.Resource<AssetServer>();
	public static implicit operator AssetServer(World world) => world.Resource<AssetServer>();
	public void ProcessAssetDrops<T>(Assets<T> assets) where T : IAsset
	{
		var reader = assets.GetHandleProvider().DropReader;
		// note that we must hold this lock for the entire duration of this function to ensure
		// that `asset_server.load` calls that occur during it block, which ensures that
		// re-loads are kicked off appropriately. This function must be "transactional" relative
		// to other asset info operations
		infoLock.EnterWriteLock();
		try {
			while (reader.TryRead(out var dropEvent)) {
				var id = dropEvent.AssetId.Typed<T>();
				if (dropEvent.IsAssetServerManaged) {
					// the process_handle_drop call checks whether new handles have been created since the drop event was fired, before removing the asset
					if (!infos.ProcessHandleDrop(id.Untyped())) {
						// a new handle has been created, or the asset doesn't exist
						continue;
					}
				}
				assets.RemoveDropped(id);
			}
		}
		finally {
			infoLock.ExitWriteLock();
		}
	}

	public LoadState GetLoadState<T>(T intoId) where T : IIntoUntypedAssetId
	{
		infoLock.EnterReadLock();
		try {
			return infos.Get(intoId.IntoUntypedAssetId())?.LoadState ?? LoadState.NotLoaded;
		}
		finally {
			infoLock.ExitReadLock();
		}
	}

	public bool IsLoaded<T>(T intoId) where T : IIntoUntypedAssetId
	{
		return GetLoadState(intoId).IsLoaded;
	}

	public static void HandleInternalAssetEvents(World world)
	{
		world.ResourceScope<AssetServer>(server => {
			server.infoLock.EnterWriteLock();
			try {
				while (server.eventReader.TryRead(out var evt)) {
					switch (evt.Type) {
						case InternalAssetEvent.EventType.Loaded:
							server.infos.ProcessAssetLoad(evt.Id, evt.LoadedAsset!, world, server.eventWriter);
							break;
						case InternalAssetEvent.EventType.LoadedWithDependencies:
							server.infos.OnDependencyloaded[evt.Id.Type](world, evt.Id);
							var assetInfo = server.infos.Get(evt.Id);
							if (assetInfo != null) {
								foreach (var notifier in assetInfo.WaitingTasks) {
									notifier.SetResult();
								}
								assetInfo.WaitingTasks.Clear();
							}
							break;
						case InternalAssetEvent.EventType.Failed:
							server.infos.ProcessAssetFail(evt.Id, evt.Exception!);
							server.infos.OnDependencyFailed[evt.Id.Type](world, evt.Id, evt.Path, evt.Exception!);
							Log.Warning("Asset load failed for {id} at {path}: {error}", evt.Id, evt.Path, evt.Exception);
							break;
					}
				}
				// TODO bevy collects all errors and publishes them to an untyped error message
				// TODO propagate file change events

				// Remove all completed pending tasks
				var completedTasks = server.infos.PendingTasks.Where(kv => kv.Value.IsCompleted).Select(kv => kv.Key).ToList();
				foreach (var key in completedTasks) {
					server.infos.PendingTasks.Remove(key);
				}

			}
			finally {
				server.infoLock.ExitWriteLock();
			}
		});
	}
}

public class AssetSources
{
	public AssetSources(IAssetSource defaultSource, Dictionary<string, IAssetSource> sources)
	{
		this.DefaultSource = defaultSource;
		this.sources = sources;
	}

	public class Builder : IDefault<Builder>
	{
		private Dictionary<string, IAssetSource> sources = new ();
		private IAssetSource? DefaultSource;
		public Builder()
		{
			DefaultSource = null;
		}

		public void AddSource(IAssetSource source, string key)
		{
			if (key == "") {
				if (DefaultSource == null) {
					throw new ArgumentException("Cannot replace default asset source with AddSource. Use ReplaceDefault.");
				}
				DefaultSource = source;
			} else if (!sources.TryAdd(key, source)) {
				throw new ArgumentException($"Source with key {key} already exists");
			}
		}

		public void ReplaceDefault(IAssetSource source)
		{
			DefaultSource = source;
		}

		public AssetSources Build()
		{
			if (DefaultSource == null) {
				DefaultSource = new FileSystemSource(".");
				Log.Information("No default asset source specified. Using FileSystemSource at root directory.");
			}
			return new AssetSources(DefaultSource, sources);
		}
		public static Builder Default() => new ();
	}

	private Dictionary<string, IAssetSource> sources;
	private IAssetSource DefaultSource;


	public IAssetSource GetSource(string source)
	{
		return string.IsNullOrEmpty(source) ? DefaultSource : sources[source];
	}
}

public interface IAssetSource
{
	public Task<Stream> Read(string path);
	public Task<Stream> ReadMeta(string path);
	public Task<bool> IsDirectory(string path);
	public Task<IEnumerable<string>> ListDirectoryContents(string path);

	// TODO write API
}

public enum AssetSourceEventType
{
	Added,
	Modified,
	Removed,
	Renamed,
}

public enum AssetSourceEventObject
{
	Unknown,
	Asset,
	Meta,
	Folder,
}

/// <summary> 
/// An asset source change event that occurs whenever an asset (or metadata) is created/added/removed
/// </summary>
public struct AssetSourceEvent
{
	public AssetSourceEvent(AssetSourceEventType type, AssetSourceEventObject objectType, string path, string? oldPath = null)
	{
		Type = type;
		ObjectType = objectType;
		Path = path;
		OldPath = oldPath;
	}

	public AssetSourceEventType Type;
	public AssetSourceEventObject ObjectType;
	public string Path;
	/// <summary>
	/// If Type is <see cref="AssetSourceEventType.Renamed" />, this will contain the old path.
	/// </summary>
	public string? OldPath;
}