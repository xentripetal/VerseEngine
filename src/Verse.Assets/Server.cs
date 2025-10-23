using System.Threading.Channels;
using Serilog;
using Verse.ECS;

namespace Verse.Assets;

public enum HandleLoadingMode
{
	/// <summary>
	/// The handle is for an asset that isn't loading/loaded yet.
	/// </summary>
	NotLoading,
	/// <summary>
	/// The handle is for an asset that is being requested to load (if it isn't already loading)
	/// </summary>
	Request,
	/// <summary>
	/// The handle is for an asset that is being forced to load (even if it has already loaded)
	/// </summary>
	Force
}

internal struct InternalAssetEvent
{
	public enum EventType
	{
		Loaded,
		LoadedWithDependencies,
		Failed,
	}

	public static InternalAssetEvent Loaded(UntypedAssetId id, UntypedLoadedAsset asset) =>
		new InternalAssetEvent { Type = EventType.Loaded, Id = id, LoadedAsset = asset };
	public static InternalAssetEvent LoadedWithDependencies(UntypedAssetId id) =>
		new InternalAssetEvent { Type = EventType.LoadedWithDependencies, Id = id };
	public static InternalAssetEvent Failed(UntypedAssetId id, AssetLoadException exception, AssetPath path) =>
		new InternalAssetEvent { Type = EventType.Failed, Id = id, Exception = exception, Path = path };

	public EventType Type;
	public UntypedAssetId Id;
	public UntypedLoadedAsset? LoadedAsset;
	public AssetPath? Path;
	public Exception? Exception;
}

public class AssetInfos
{
	private Dictionary<AssetPath, Dictionary<Type, UntypedAssetId>> pathToId = new ();
	private Dictionary<UntypedAssetId, AssetInfo> infos = new ();
	/// <summary>
	/// If set to true, this will track data relevant to watching for changes (such as <see cref="LoadDependents"/>
	/// This should only be set at startup
	/// </summary>
	internal bool WatchingForChanges;

	internal Dictionary<AssetPath, HashSet<AssetPath>> LoaderDependents = new ();
	internal Dictionary<AssetPath, HashSet<string>> LivingLabeledAssets = new ();
	internal Dictionary<Type, AssetHandleProvider> HandleProviders = new ();
	internal Dictionary<Type, Action<World, UntypedAssetId>> OnDependencyloaded = new ();
	internal Dictionary<Type, Action<World, UntypedAssetId, AssetPath?, AssetLoadException>> OnDependencyFailed = new ();
	internal Dictionary<UntypedAssetId, Task> PendingTasks = new ();

	internal UntypedHandle CreateLoadingHandleUntyped(Type type)
	{
		return CreateHandleInternal(type, null, null, true);
	}

	private UntypedHandle CreateHandleInternal(Type type, AssetPath? path, Action<IAssetMeta>? metaTransform, bool loading)
	{
		var provider = HandleProviders[type];
		if (WatchingForChanges && path.HasValue && path.Value.Label != null) {
			if (!LivingLabeledAssets.TryGetValue(path.Value.WithoutLabel(), out var labels)) {
				labels = new HashSet<string>();
				LivingLabeledAssets[path.Value.WithoutLabel()] = labels;
			}
			labels.Add(path.Value.Label);
		}

		var handle = provider.ReserveHandleInternal(true, path, metaTransform);
		var info = new AssetInfo(new WeakReference<StrongHandle>(handle), path);
		if (loading) {
			info.LoadState = LoadState.Loading;
			info.DependencyLoadState = LoadState.Loading;
			info.RecursiveDependencyLoadState = LoadState.Loading;
		}
		infos[handle.Id] = info;
		return new UntypedHandle(handle);
	}

	internal (Handle<T> Handle, bool Loading) GetOrCreatePathHandle<T>(AssetPath path, HandleLoadingMode mode, Action<IAssetMeta>? metaTransform)
		where T : IAsset
	{
		var (handle, loading) = GetOrCreatePathHandleInternal(path, typeof(T), mode, metaTransform);
		return (handle.Typed<T>(), loading);
	}

	internal (UntypedHandle Handle, bool Loading) GetOrCreatePathHandleUntyped(
		AssetPath path, Type type, HandleLoadingMode mode, Action<IAssetMeta>? metaTransform)
	{
		return GetOrCreatePathHandleInternal(path, type, mode, metaTransform);
	}

	internal (UntypedHandle Handle, bool Loading) GetOrCreatePathHandleInternal(
		AssetPath path, Type? type, HandleLoadingMode mode, Action<IAssetMeta>? metaTransform)
	{
		if (!pathToId.TryGetValue(path, out var handles)) {
			handles = new Dictionary<Type, UntypedAssetId>();
			pathToId[path] = handles;
		}

		if (type == null) {
			// if we have a single TypeId, we can infer it
			if (handles.Count == 1) {
				type = handles.First().Key;
			} else {
				throw new ArgumentException("Handle does not exist but Type is null");
			}
		}

		if (handles.TryGetValue(type, out var id)) {
			var info = infos[id];
			var shouldLoad = false;
			if (mode == HandleLoadingMode.Force ||
			    (mode == HandleLoadingMode.Request && info.LoadState.Status is LoadState.LoadStatus.NotLoaded or LoadState.LoadStatus.Failed)) {
				info.LoadState = LoadState.Loading;
				info.DependencyLoadState = LoadState.Loading;
				info.RecursiveDependencyLoadState = LoadState.Loading;
				shouldLoad = true;
			}

			if (info.WeakHandle.TryGetTarget(out var handle)) {
				// If we can get the handle, there is at least one live handle right now, the asset load has already 
				// kicked off (and maybe completed), so we can just return the handle
				return (new UntypedHandle(handle), shouldLoad);
			}
			// Asset already exists but all handles were dropped. This mean the TrackAssets system hasn't been run yet 
			// to remove the current asset.

			// We must create a new strong handle for the existing id and ensure that the drop of the old strong handle
			// doesn't remove the asset from the assets collection
			info.HandleDropsToSkip++;
			var provider = HandleProviders[type];
			handle = provider.GetHandle(id.Id, true, path, metaTransform);
			info.WeakHandle = new WeakReference<StrongHandle>(handle);
			return (new UntypedHandle(handle), shouldLoad);
		} else {
			var shouldLoad = mode is HandleLoadingMode.Request or HandleLoadingMode.Force;
			var handle = CreateHandleInternal(type, path, metaTransform, shouldLoad);
			handles[type] = handle.Id();
			return (handle, shouldLoad);
		}
	}

	internal AssetInfo? Get(UntypedAssetId id) => infos.TryGetValue(id, out var info) ? info : null;
	internal bool ContainsKey(UntypedAssetId id) => infos.ContainsKey(id);
	internal UntypedHandle? GetPathAndTypeHandle(AssetPath path, Type type)
	{
		if (!pathToId.TryGetValue(path, out var id)) {
			return null;
		}
		if (!id.TryGetValue(type, out var handleId)) {
			return null;
		}
		return GetIdHandle(handleId);
	}

	internal IEnumerable<UntypedAssetId> GetPathIds(AssetPath path)
	{
		if (pathToId.TryGetValue(path, out var id)) {
			return id.Values;
		}
		return Enumerable.Empty<UntypedAssetId>();
	}

	internal IEnumerable<UntypedHandle> GetPathHandles(AssetPath path)
	{
		return GetPathIds(path).Select(GetIdHandle).Where(x => x.HasValue).Select(x => x!.Value);
	}

	internal UntypedHandle? GetIdHandle(UntypedAssetId id)
	{
		if (infos.TryGetValue(id, out var info)) {
			if (info.WeakHandle.TryGetTarget(out var handle)) {
				return new UntypedHandle(handle);
			}
		}
		return null;
	}

	internal bool IsPathAlive<TInto>(TInto path) where TInto : IIntoAssetPath
	{
		return GetPathIds(path.IntoAssetPath()).Any(id => infos.TryGetValue(id, out var info) && info.WeakHandle.TryGetTarget(out _));
	}

	internal bool ShouldReload(AssetPath path)
	{
		if (IsPathAlive(path)) {
			return true;
		}
		if (LivingLabeledAssets.TryGetValue(path, out var labels)) {
			return labels.Count > 0;
		}
		return false;
	}

	/// <summary>
	/// Returns true if the asset should be removed the collection
	/// </summary>
	/// <returns></returns>
	internal bool ProcessHandleDrop(UntypedAssetId id)
	{
		if (!infos.TryGetValue(id, out var info)) {
			return false;
		}
		if (info.HandleDropsToSkip > 0) {
			info.HandleDropsToSkip--;
			return false;
		}
		PendingTasks.Remove(id);
		infos.Remove(id);
		if (info.Path == null) {
			return true;
		}
		if (WatchingForChanges) {
			RemoveDependentsAndLabels(info, info.Path.Value);
		}
		if (pathToId.TryGetValue(info.Path.Value, out var map)) {
			map.Remove(id.Type);
			if (map.Count == 0) {
				pathToId.Remove(info.Path.Value);
			}
		}
		return true;
	}

	internal void ProcessAssetLoad(UntypedAssetId id, UntypedLoadedAsset loadedAsset, World world, ChannelWriter<InternalAssetEvent> writer)
	{
		if (!infos.ContainsKey(id)) {
			return;
		}
		loadedAsset.Value.InsertAsset(id, world);
		var loadingDeps = loadedAsset.Dependencies;
		var failedDeps = new HashSet<UntypedAssetId>();
		Exception? dependencyError = null;
		var loadingRecursiveDeps = new HashSet<UntypedAssetId>(loadedAsset.Dependencies);
		var failedRecursiveDeps = new HashSet<UntypedAssetId>();
		Exception? recursiveDependencyError = null;

		loadingDeps.RemoveWhere(depId => {
			if (!infos.TryGetValue(depId, out var depInfo)) {
				Log.Warning(
					"Dependency {dependency} from asset {asset} is unknown. This asset's dependency load status will not switch to Loaded until the unknown dependency is loaded.",
					depId, id);
				return false;
			}
			switch (depInfo.RecursiveDependencyLoadState.Status) {
				case LoadState.LoadStatus.NotLoaded or LoadState.LoadStatus.Loading:
					depInfo.DependentsWaitingOnRecursiveLoad.Add(id);
					break;
				case LoadState.LoadStatus.Loaded:
					loadingRecursiveDeps.Remove(depId);
					break;
				case LoadState.LoadStatus.Failed:
					if (recursiveDependencyError == null) {
						recursiveDependencyError = depInfo.RecursiveDependencyLoadState.Exception;
					}
					failedRecursiveDeps.Add(depId);
					loadingRecursiveDeps.Remove(depId);
					break;
			}
			switch (depInfo.LoadState.Status) {
				case LoadState.LoadStatus.NotLoaded or LoadState.LoadStatus.Loading:
					depInfo.DependentsWaitingOnLoad.Add(id);
					break;
				case LoadState.LoadStatus.Loaded:
					return true;
				case LoadState.LoadStatus.Failed:
					if (dependencyError == null) {
						dependencyError = depInfo.LoadState.Exception;
					}
					failedDeps.Add(depId);
					return true;
			}
			return false;
		});

		var dependencyLoadState = LoadState.Loading;
		if (loadingDeps.Count == 0 && failedDeps.Count == 0) {
			dependencyLoadState = LoadState.Loaded;
		} else if (failedDeps.Count == 0) {
			dependencyLoadState = LoadState.Failed(dependencyError!);
			;
		}

		var recursiveDependencyLoadState = LoadState.Loading;
		if (loadingRecursiveDeps.Count == 0 && failedRecursiveDeps.Count == 0) {
			if (!writer.TryWrite(InternalAssetEvent.LoadedWithDependencies(id))) {
				throw new AssetLoadException("internal asset event channel blocked");
			}
			recursiveDependencyLoadState = LoadState.Loaded;
		} else if (failedRecursiveDeps.Count == 0) {
			recursiveDependencyLoadState = LoadState.Failed(recursiveDependencyError!);
		}

		HashSet<UntypedAssetId>? dependentsWaitingOnRecursiveLoad;

		var info = infos[id];
		// if watching for changes, track reverse loader deps for hot reloading
		if (WatchingForChanges) {
			if (info.Path.HasValue) {
				foreach (var loaderDep in loadedAsset.LoaderDependencies.Keys) {
					if (!LoaderDependents.TryGetValue(loaderDep, out var dependents)) {
						dependents = new HashSet<AssetPath>();
						LoaderDependents[loaderDep] = dependents;
					}
					dependents.Add(info.Path.Value);
				}
			}
		}

		info.LoadingDependencies = loadingDeps;
		info.FailedDependencies = failedDeps;
		info.LoadingRecursiveDependencies = loadingRecursiveDeps;
		info.FailedRecursiveDependencies = failedRecursiveDeps;
		info.LoadState = LoadState.Loaded;
		info.DependencyLoadState = dependencyLoadState;
		info.RecursiveDependencyLoadState = recursiveDependencyLoadState;
		if (WatchingForChanges) {
			info.LoaderDependencies = loadedAsset.LoaderDependencies;
		}

		var waitingOnRecursiveLoad = info.DependentsWaitingOnRecursiveLoad;
		var waitingOnLoad = info.DependentsWaitingOnLoad;
		info.DependentsWaitingOnRecursiveLoad = new HashSet<UntypedAssetId>();
		info.DependentsWaitingOnLoad = new HashSet<UntypedAssetId>();

		foreach (var depId in waitingOnLoad) {
			if (infos.TryGetValue(depId, out var depInfo)) {
				depInfo.LoadingDependencies.Remove(id);
				if (depInfo.LoadingDependencies.Count == 0 && !depInfo.DependencyLoadState.IsFailed) {
					info.DependencyLoadState = LoadState.Loaded;
				}
			}
		}

		switch (recursiveDependencyLoadState.Status) {
			case LoadState.LoadStatus.Loaded:
				foreach (var depId in waitingOnRecursiveLoad) {
					PropagateLoadedState(id, depId, writer);
				}
				break;
			case LoadState.LoadStatus.Failed:
				foreach (var depId in waitingOnRecursiveLoad) {
					PropagateFailedState(id, depId, recursiveDependencyError);
				}
				break;
			default:
				if (waitingOnRecursiveLoad.Count > 0) {
					throw new AssetLoadException("Internal error: waiting on recursive load but recursive load state is not loaded or failed");
				}
				break;
		}
	}

	private void PropagateLoadedState(UntypedAssetId loadedId, UntypedAssetId waitingId, ChannelWriter<InternalAssetEvent> writer)
	{
		if (infos.TryGetValue(waitingId, out var info)) {
			info.LoadingRecursiveDependencies.Remove(loadedId);
			if (info.LoadingRecursiveDependencies.Count == 0 && info.FailedRecursiveDependencies.Count == 0) {
				info.RecursiveDependencyLoadState = LoadState.Loaded;
				if (info.LoadState.IsLoaded) {
					if (!writer.TryWrite(InternalAssetEvent.LoadedWithDependencies(waitingId))) {
						throw new AssetLoadException("internal asset event channel blocked");
					}
				}

				var waitingOnRecursiveLoad = info.DependentsWaitingOnRecursiveLoad;
				info.DependentsWaitingOnRecursiveLoad = new HashSet<UntypedAssetId>();

				foreach (var depId in waitingOnRecursiveLoad) {
					PropagateLoadedState(waitingId, depId, writer);
					;
				}
			}
		}
	}

	private void PropagateFailedState(UntypedAssetId failedId, UntypedAssetId waitingId, Exception? error)
	{
		if (infos.TryGetValue(waitingId, out var info)) {
			info.LoadingRecursiveDependencies.Remove(failedId);
			info.FailedRecursiveDependencies.Add(failedId);
			info.RecursiveDependencyLoadState = LoadState.Failed(error!);
			
			var waitingOnRecursiveLoad = info.DependentsWaitingOnRecursiveLoad;
			info.DependentsWaitingOnRecursiveLoad = new HashSet<UntypedAssetId>();
			foreach (var depId in waitingOnRecursiveLoad) {
				PropagateFailedState(waitingId, depId, error);
			}
		}
	}



	internal void ProcessAssetFail(UntypedAssetId failedId, Exception error)
	{
		if (!infos.TryGetValue(failedId, out var info)) {
			return;
		}
		
		info.LoadState = LoadState.Failed(error);
		info.DependencyLoadState = LoadState.Failed(error);
		info.RecursiveDependencyLoadState = LoadState.Failed(error);
		foreach (var task in info.WaitingTasks) {
			task.SetException(error);
		}
		var dependentsWaitingOnLoad = info.DependentsWaitingOnLoad;
		var dependentsWaitingOnRecursiveLoad = info.DependentsWaitingOnRecursiveLoad;
		info.DependentsWaitingOnLoad = new HashSet<UntypedAssetId>();
		info.DependentsWaitingOnRecursiveLoad = new HashSet<UntypedAssetId>();

		foreach (var waitingId in dependentsWaitingOnLoad) {
			info.LoadingDependencies.Remove(failedId);
			info.FailedDependencies.Add(failedId);
			if (!info.DependencyLoadState.IsFailed) {
				info.DependencyLoadState = LoadState.Failed(error);
			}
		}
		
		foreach (var depId in dependentsWaitingOnRecursiveLoad) {
			PropagateFailedState(failedId, depId, error);
		}
	}

	internal void RemoveDependentsAndLabels(AssetInfo info, AssetPath path)
	{
		foreach (var loaderDep in info.LoaderDependencies.Keys) {
			if (LoaderDependents.TryGetValue(loaderDep, out var dependents)) {
				dependents.Remove(path);
			}
		}
		if (path.Label == null) {
			return;
		}
		var withoutLabel = path.WithoutLabel();
		if (!LivingLabeledAssets.TryGetValue(withoutLabel, out var labels)) return;
		labels.Remove(path.Label);
		if (labels.Count == 0) {
			LivingLabeledAssets.Remove(withoutLabel);
		}
	}

	/// <summary>
	/// Consumes all current handle drop events. This will update information in <see cref="AssetInfos"/> , but it
	/// wont affect <see cref="Assets"/> storages. For normal uses cases, prefer <see cref="Assets{T}.TrackAssets"/>
	/// This hould only be called if Assets storage isn't being used (such as in <see cref="AssetProcessor"/>
	/// </summary>
	internal void ConsumeHandleDropEvents()
	{
		foreach (var provider in HandleProviders.Values) {
			while (provider.DropReader.TryRead(out var dropEvent)) {
				if (dropEvent.IsAssetServerManaged) {
					ProcessHandleDrop(dropEvent.AssetId.Untyped(provider.Type));
				}
			}
		}
	}

}

public class AssetLoadException : Exception
{
	public AssetLoadException(string message) : base(message) { }
	public AssetLoadException(string message, Exception? innerException) : base(message, innerException) { }
}

public struct LoadState
{
	public enum LoadStatus
	{
		NotLoaded,
		Loading,
		Loaded,
		Failed
	}

	public LoadState(Exception? exception)
	{
		Exception = exception;
		Status = LoadStatus.Failed;
	}

	public LoadState(LoadStatus status)
	{
		Status = status;
		Exception = null;
	}

	public static LoadState Failed(Exception exception) => new (exception);
	public static LoadState NotLoaded => new (LoadStatus.NotLoaded);
	public static LoadState Loading => new (LoadStatus.Loading);
	public static LoadState Loaded => new LoadState(LoadStatus.Loaded);

	public LoadStatus Status;
	public Exception? Exception;

	public bool IsFailed => Status == LoadStatus.Failed;
	public bool IsLoaded => Status == LoadStatus.Loaded;
}

public class AssetInfo
{
	public AssetInfo(WeakReference<StrongHandle> weakHandle, AssetPath? path)
	{
		WeakHandle = weakHandle;
		Path = path;
	}
	internal WeakReference<StrongHandle> WeakHandle;
	internal AssetPath? Path;
	internal LoadState LoadState = LoadState.NotLoaded;
	internal LoadState DependencyLoadState = LoadState.NotLoaded;
	internal LoadState RecursiveDependencyLoadState = LoadState.NotLoaded;
	internal HashSet<UntypedAssetId> LoadingDependencies = new ();
	internal HashSet<UntypedAssetId> FailedDependencies = new ();
	internal HashSet<UntypedAssetId> LoadingRecursiveDependencies = new ();
	internal HashSet<UntypedAssetId> FailedRecursiveDependencies = new ();
	internal HashSet<UntypedAssetId> DependentsWaitingOnLoad = new ();
	internal HashSet<UntypedAssetId> DependentsWaitingOnRecursiveLoad = new ();
	internal Dictionary<AssetPath, AssetHash> LoaderDependencies = new ();
	internal uint HandleDropsToSkip = 0;
	internal List<TaskCompletionSource> WaitingTasks = new ();
}