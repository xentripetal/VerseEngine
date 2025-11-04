using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Assets;

public record struct AssetPath : IIntoAssetPath
{
	public string Source;
	public string Path;
	public string? Label;

	public AssetPath WithoutLabel() => new AssetPath {
		Source = Source,
		Path = Path,
	};

	public AssetPath WithLabel(string label) => new AssetPath {
		Source = Source,
		Path = Path,
		Label = label,
	};

	public AssetPath IntoAssetPath() => this;
	public static AssetPath ParseUri(string inputPath)
	{
		var source = "";
		var path = inputPath;
		var parts = inputPath.Split("://");
		if (parts.Length > 1) {
			source = parts[0];
			path = parts[1];
		}

		parts = path.Split("#");
		string? label = null;
		if (parts.Length > 1) {
			label = parts[0];
			source = parts[1];
		}
		return new AssetPath {
			Source = source,
			Path = path,
			Label = label,
		};
	}
}

public interface IIntoAssetPath
{
	AssetPath IntoAssetPath();
}

public record struct AssetHash
{
	public int Hash;
}

public interface IAsset : IVisitAssetDependencies, IAssetContainer
{
	void IVisitAssetDependencies.VisitDependencies(Action<UntypedAssetId> visit)
	{
		// Default to no dependencies
	}
}

public interface IAsset<out TSelf> : IAsset where TSelf : IAsset<TSelf>
{
	/// <summary>
	/// Insert the asset in this container into the worlds Assets.
	/// </summary>
	void IAssetContainer.InsertAsset(UntypedAssetId id, World world)
	{
		world.Resource<Assets<TSelf>>().Insert(id.Typed<TSelf>(), GetValue());
	}
	Type IAssetContainer.AssetType { get => typeof(TSelf); }
	protected TSelf GetValue() => (TSelf)this;
}

public interface IVisitAssetDependencies
{
	/// <summary>
	/// Defines how to visit the dependencies of this asset. 
	/// </summary>
	/// <remarks>For example, a 3d model might require both textures and meshes to be loaded</remarks>
	public void VisitDependencies(Action<UntypedAssetId> visit);
}

public sealed class DenseAssetStorage<T> where T : IAsset
{
	public DenseAssetStorage()
	{
		storage = new List<Entry>();
		allocator = new AssetIndexAllocator();
	}

	public struct Entry
	{
		public T? Value;
		public uint Generation;

		public const uint INVALID_GENERATION = 0;
	}

	internal List<Entry> storage;
	private int length;
	private AssetIndexAllocator allocator;

	internal AssetIndexAllocator Allocator => allocator;

	public int Length => length;
	public bool IsEmpty => length == 0;
	/// <summary>
	/// Inserts the value at the given index. Returns true if a value already exists (and was replaced)
	/// </summary>
	public bool Insert(AssetIndex index, T asset)
	{
		Flush();
		if ((int)index.Index >= storage.Count) {
			throw new InvalidGenerationException(index);
		}

		var entry = storage[(int)index.Index];
		if (entry.Generation != index.Generation) {
			throw new InvalidGenerationException(index, entry.Generation);
		}

		var hadValue = entry.Value != null;
		if (!hadValue) {
			length++;
		}
		entry.Value = asset;
		storage[(int)index.Index] = entry;
		return hadValue;
	}

	/// <summary>
	/// Removes the asset stored at the given index and returns the value that was there. This will recycle the id and
	/// allow new entries to be inserted.
	/// </summary>
	internal T? RemoveDropped(AssetIndex index)
	{
		var value = RemoveStillAlive(index);
		// Drop the entry entirely
		storage[(int)index.Index] = new Entry();
		allocator.Recycle(index);
		return value;
	}

	/// <summary>
	/// Removes the asset stored at the given index and returns it (if the asset exists).
	/// This will not recycle the id. New values with the current id can still be inserted. The id will not be reused until
	/// <see cref="RemoveDropped"/> is called;
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	internal T? RemoveStillAlive(AssetIndex index)
	{
		Flush();
		var actualIndex = (int)index.Index;
		if (actualIndex >= length) return default;
		var entry = storage[actualIndex];
		if (entry.Generation == index.Generation) {
			storage[actualIndex] = new Entry {
				Generation = entry.Generation,
			};
			length--;
		}
		return entry.Value;
	}

	public T? Get(AssetIndex index)
	{
		var actualIndex = (int)index.Index;
		if (actualIndex >= storage.Count) return default;
		var entry = storage[actualIndex];
		if (entry.Generation != index.Generation) return default;
		return entry.Value;
	}

	public bool TryGet(AssetIndex index, out T? value)
	{
		var actualIndex = (int)index.Index;
		if (actualIndex >= storage.Count) {
			value = default;
			return false;
		}
		var entry = storage[actualIndex];
		if (entry.Generation != index.Generation) {
			value = default;
			return false;
		}
		value = entry.Value;
		return true;
	}

	internal void Flush()
	{
		var newLength = allocator.nextIndex;
		if (newLength > storage.Count) {
			for (var i = storage.Count; i < newLength; i++) {
				storage.Add(new Entry {
					Generation = 1,
				});
			}
		} else if (newLength < storage.Count) {
			storage.RemoveRange((int)newLength, (int)(storage.Count - newLength));
		}

		while (allocator.RecycledReceiver.TryRead(out var index)) {
			storage[(int)index.Index] = new Entry {
				Generation = index.Generation,
			};
		}
	}
}

public enum AssetEventType
{
	/// <summary>Emitted whenever an <see cref="IAsset"/> is added.</summary>
	Added,
	/// <summary>Emitted whenever an <see cref="IAsset"/> value is modified.</summary>
	Modified,
	/// <summary>Emitted whenever an <see cref="IAsset"/> is removed. </summary>
	Removed,
	/// <summary>Emitted when the last <see cref="StrongHandle"/> of an <see cref="IAsset"/> is garbage collected</summary>
	Unused,
	/// <summary>Emitted whenever an <see cref="IAsset"/> has been fully loaded (including its deps and all its recursive deps</summary>
	LoadedWithDependencies,
}

public record struct AssetEvent<T> where T : IAsset
{
	public AssetEventType Type;
	public AssetId<T> Id;
}

public record struct AssetLoadFailedEvent<T> where T : IAsset
{
	public AssetId<T> Id;
	public AssetPath Path;
	public AssetLoadException Error;
}

/// <summary>
/// Stores <see cref="IAsset"/> values identified by their <see cref="AssetId{T}"/>
/// </summary>
/// <remarks>
/// Assets identified by <see cref="AssetIndex"/> will be stored in a "dense" vec-like storage. This is more efficient, but it means that
/// the assets can only be identified at runtime. This is the default behavior.
///
/// Assets identified by <see cref="Guid"/> will be stored in a hashmap. This is less efficient, but it means that the assets can be referenced
/// at compile time.
///
/// This tracks (and queues) <see cref="AssetEvent{T}"/> events whenever changes to the collection occur.
/// To check whether the asset used by a given component has changed (due to a change in the handle or the underlying asset)
/// use the <see cref="AssetChanged"/> query filter
/// </remarks>
/// <typeparam name="T"></typeparam>
/// <summary>
/// An error returned when an <see cref="AssetIndex"/> has an invalid generation.
/// </summary>
public class InvalidGenerationException : Exception
{
	public AssetIndex Index { get; }
	public uint? CurrentGeneration { get; }

	public InvalidGenerationException(AssetIndex index, uint currentGeneration)
		: base($"AssetIndex {index} has an invalid generation. The current generation is: '{currentGeneration}'.")
	{
		Index = index;
		CurrentGeneration = currentGeneration;
	}

	public InvalidGenerationException(AssetIndex index)
		: base($"AssetIndex {index} has been removed")
	{
		Index = index;
		CurrentGeneration = null;
	}
}

/// <summary>
/// Stores <see cref="IAsset"/> values identified by their <see cref="AssetId{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Assets identified by <see cref="AssetIndex"/> will be stored in a "dense" vec-like storage. This is more efficient, but it means that
/// the assets can only be identified at runtime. This is the default behavior.
/// </para>
/// <para>
/// Assets identified by <see cref="Guid"/> will be stored in a hashmap. This is less efficient, but it means that the assets can be referenced
/// at compile time.
/// </para>
/// <para>
/// This tracks (and queues) <see cref="AssetEvent{T}"/> events whenever changes to the collection occur.
/// To check whether the asset used by a given component has changed (due to a change in the handle or the underlying asset)
/// use the <see cref="AssetChanged"/> query filter
/// </para>
/// <para>
/// Based on Bevy's Assets implementation from bevy_asset/src/assets.rs
/// </para>
/// </remarks>
/// <typeparam name="T">The asset type</typeparam>
public partial class Assets<T>  
	where T : IAsset 
{
	public Assets()
	{
		DenseStorage = new DenseAssetStorage<T>();
		HandleProvider = new AssetHandleProvider(typeof(T), DenseStorage.Allocator);
		HashStorage = new Dictionary<Guid, T>();
		QueuedEvents = new List<AssetEvent<T>>();
		DuplicateHandles = new Dictionary<AssetId<T>, ushort>();
	}

	private DenseAssetStorage<T> DenseStorage;
	private Dictionary<Guid, T> HashStorage;
	private AssetHandleProvider HandleProvider;
	private List<AssetEvent<T>> QueuedEvents;
	/// <summary>
	/// Assets managed by this with live strong <see cref="Handle{T}"/>s originating from <see cref="GetStrongHandle"/>
	/// </summary>
	public Dictionary<AssetId<T>, ushort> DuplicateHandles;

	/// <summary>
	/// Retrieves an <see cref="AssetHandleProvider"/> capable of reserving new <see cref="Handle{T}"/> values for assets that will be stored in this
	/// collection.
	/// </summary>
	public AssetHandleProvider GetHandleProvider() => HandleProvider;

	/// <summary>
	/// Reserves a new <see cref="Handle{T}"/> for an asset that will be stored in this collection.
	/// </summary>
	public Handle<T> ReserveHandle()
	{
		return HandleProvider.ReserveHandle().Typed<T>();
	}

	/// <summary>
	/// Inserts the given <paramref name="asset"/>, identified by the given <paramref name="id"/>. If an asset already exists for
	/// <paramref name="id"/>, it will be replaced.
	/// </summary>
	/// <param name="id">The asset ID</param>
	/// <param name="asset">The asset to insert</param>
	/// <exception cref="InvalidGenerationException">Thrown when the asset index has an invalid generation</exception>
	/// <remarks>Note: This will never throw an exception for UUID asset IDs.</remarks>
	public void Insert(AssetId<T> id, T asset)
	{
		if (id.Id.IsGuid) {
			InsertWithGuid(id.Id.AsGuid, asset);
		} else {
			InsertWithIndex(id.Id.AsIndex, asset);
		}
	}

	/// <summary>
	/// Retrieves an <see cref="IAsset"/> stored for the given <paramref name="id"/> if it exists. If it does not exist, it will
	/// be inserted using <paramref name="insertFn"/>.
	/// </summary>
	/// <param name="id">The asset ID</param>
	/// <param name="insertFn">Function to create the asset if it doesn't exist</param>
	/// <returns>The asset value</returns>
	/// <exception cref="InvalidGenerationException">Thrown when the asset index has an invalid generation</exception>
	/// <remarks>Note: This will never throw an exception for UUID asset IDs.</remarks>
	public T GetOrInsertWith(AssetId<T> id, Func<T> insertFn)
	{
		var existing = Get(id);
		if (existing == null) {
			var newAsset = insertFn();
			Insert(id, newAsset);
			return newAsset;
		}
		return existing;
	}

	/// <summary>
	/// Returns <c>true</c> if the <paramref name="id"/> exists in this collection. Otherwise it returns <c>false</c>.
	/// </summary>
	public bool Contains(AssetId<T> id)
	{
		if (id.Id.IsGuid) {
			return HashStorage.ContainsKey(id.Id.AsGuid);
		} else {
			return DenseStorage.Get(id.Id.AsIndex) != null;
		}
	}

	/// <summary>
	/// Returns <c>true</c> if the <paramref name="handle"/> exists in this collection. Otherwise it returns <c>false</c>.
	/// </summary>
	public bool Contains(Handle<T> handle) => Contains(handle.Id());

	private T? InsertWithGuid(Guid guid, T asset)
	{
		var result = HashStorage.TryGetValue(guid, out var existing) ? existing : default;
		HashStorage[guid] = asset;

		if (existing != null) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Modified, Id = new AssetId<T>(guid) });
		} else {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Added, Id = new AssetId<T>(guid) });
		}
		return result;
	}

	private bool InsertWithIndex(AssetIndex index, T asset)
	{
		var replaced = DenseStorage.Insert(index, asset);
		if (replaced) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Modified, Id = new AssetId<T>(index) });
		} else {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Added, Id = new AssetId<T>(index) });
		}
		return replaced;
	}

	/// <summary>
	/// Adds the given <paramref name="asset"/> and allocates a new strong <see cref="Handle{T}"/> for it.
	/// </summary>
	public Handle<T> Add(T asset)
	{
		var index = DenseStorage.Allocator.Reserve();
		InsertWithIndex(index, asset);
		return new Handle<T>(HandleProvider.GetHandle(new AssetIndexOrGuid(index), false, null, null));
	}

	/// <summary>
	/// Upgrade an <see cref="AssetId{T}"/> into a strong <see cref="Handle{T}"/> that will prevent asset drop.
	/// </summary>
	/// <param name="id">The asset ID to upgrade</param>
	/// <returns>A strong handle, or <c>null</c> if the provided <paramref name="id"/> is not part of this collection</returns>
	/// <remarks>Returns <c>null</c> if the provided <paramref name="id"/> is not part of this Assets collection.
	/// For example, it may have been dropped earlier.</remarks>
	public Handle<T>? GetStrongHandle(AssetId<T> id)
	{
		if (!Contains(id)) {
			return null;
		}

		DuplicateHandles[id] = (ushort)((DuplicateHandles.TryGetValue(id, out var count) ? count : 0) + 1);

		return new Handle<T>(HandleProvider.GetHandle(id.Id, false, null, null));
	}

	/// <summary>
	/// Retrieves a reference to the <see cref="IAsset"/> with the given <paramref name="id"/>, if it exists.
	/// </summary>
	/// <param name="id">The asset ID</param>
	/// <returns>The asset if it exists, otherwise <c>null</c></returns>
	public T? Get<TInto>(TInto intoId) where TInto : IIntoAssetId<T>
	{
		var id = intoId.IntoAssetId();
		if (id.Id.IsGuid) {
			return HashStorage.TryGetValue(id.Id.AsGuid, out var value) ? value : default(T?);
		} else {
			return DenseStorage.TryGet(id.Id.AsIndex, out var value) ? value : default(T?);
		}
	}

	public bool TryGet<TInto>(TInto intoId, out T? value) where TInto : IIntoAssetId<T>
	{
		var id = intoId.IntoAssetId();
		if (id.Id.IsGuid) {
			return HashStorage.TryGetValue(id.Id.AsGuid, out value);
		}
		return DenseStorage.TryGet(id.Id.AsIndex, out value);
	}


	/// <summary>
	/// Retrieves a mutable reference to the <see cref="IAsset"/> with the given <paramref name="id"/>, if it exists.
	/// </summary>
	/// <param name="id">The asset ID. Supports anything that can convert to <see cref="AssetId{T}"/>, which includes <see cref="Handle{T}"/> and <see cref="AssetId{T}"/>.</param>
	/// <returns>The asset if it exists, otherwise <c>null</c></returns>
	/// <remarks>This will emit an <see cref="AssetEventType.Modified"/> event if the asset exists.</remarks>
	public T? GetMut<TInto>(TInto intoId) where TInto : IIntoAssetId<T>
	{
		var result = GetMutUntracked(intoId);
		if (result != null) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Modified, Id = intoId.IntoAssetId() });
		}
		return result;
	}


	/// <summary>
	/// Retrieves a mutable reference to the <see cref="IAsset"/> with the given <paramref name="id"/>, if it exists.
	/// This is the same as <see cref="GetMut"/> except it doesn't emit <see cref="AssetEventType.Modified"/>.
	/// </summary>
	public T? GetMutUntracked<TInto>(TInto id) where TInto : IIntoAssetId<T>
	{
		var actualId = id.IntoAssetId();
		if (actualId.Id.IsGuid) {
			return HashStorage.TryGetValue(actualId.Id.AsGuid, out var value) ? value : default;
		}
		return DenseStorage.Get(actualId.Id.AsIndex);
	}


	/// <summary>
	/// Removes (and returns) the <see cref="IAsset"/> with the given <paramref name="id"/>, if it exists.
	/// </summary>
	/// <param name="id">The asset ID</param>
	/// <returns>The removed asset if it existed, otherwise <c>null</c></returns>
	public T? Remove<TInto>(TInto intoId) where TInto : IIntoAssetId<T>
	{
		var id = intoId.IntoAssetId();
		var result = RemoveUntracked(id);
		if (result != null) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Removed, Id = id });
		}
		return result;
	}

	/// <summary>
	/// Removes (and returns) the <see cref="IAsset"/> with the given <paramref name="id"/>, if it exists. This skips emitting <see cref="AssetEventType.Removed"/>.
	/// This is the same as <see cref="Remove"/> except it doesn't emit <see cref="AssetEventType.Removed"/>.
	/// </summary>
	public T? RemoveUntracked<TInto>(TInto intoId) where TInto : IIntoAssetId<T>
	{
		var id = intoId.IntoAssetId();
		DuplicateHandles.Remove(id);

		if (id.Id.IsGuid) {
			HashStorage.Remove(id.Id.AsGuid, out var value);
			return value;
		} else {
			return DenseStorage.RemoveStillAlive(id.Id.AsIndex);
		}
	}


	/// <summary>
	/// Internal method to remove assets when handles are dropped
	/// </summary>
	internal void RemoveDropped(AssetId<T> id)
	{
		if (DuplicateHandles.TryGetValue(id, out var count)) {
			if (count == 0) {
				DuplicateHandles.Remove(id);
			} else {
				DuplicateHandles[id] = (ushort)(count - 1);
				return;
			}
		}

		bool existed;
		if (id.Id.IsGuid) {
			existed = HashStorage.Remove(id.Id.AsGuid);
		} else {
			existed = DenseStorage.RemoveDropped(id.Id.AsIndex) != null;
		}

		QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Unused, Id = id });
		if (existed) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Removed, Id = id });
		}
	}

	/// <summary>
	/// Returns <c>true</c> if there are no assets in this collection.
	/// </summary>
	public bool IsEmpty => DenseStorage.IsEmpty && HashStorage.Count == 0;

	/// <summary>
	/// Returns the number of assets currently stored in the collection.
	/// </summary>
	public int Length => DenseStorage.Length + HashStorage.Count;

	/// <summary>
	/// Returns an iterator over the <see cref="AssetId{T}"/> of every <see cref="IAsset"/> stored in this collection.
	/// </summary>
	public IEnumerable<AssetId<T>> Ids()
	{
		// Return IDs from dense storage
		for (int i = 0; i < DenseStorage.storage.Count; i++) {
			var entry = DenseStorage.storage[i];
			if (entry.Value != null) {
				yield return new AssetId<T>(new AssetIndex(entry.Generation, (uint)i));
			}
		}

		// Return IDs from hash storage
		foreach (var kvp in HashStorage) {
			yield return new AssetId<T>(kvp.Key);
		}
	}

	/// <summary>
	/// Returns an iterator over the <see cref="AssetId{T}"/> and <see cref="IAsset"/> ref of every asset in this collection.
	/// </summary>
	public IEnumerable<(AssetId<T> Id, T Asset)> Iter()
	{
		// Return assets from dense storage
		for (int i = 0; i < DenseStorage.storage.Count; i++) {
			var entry = DenseStorage.storage[i];
			if (entry.Value != null) {
				var id = new AssetId<T>(new AssetIndex(entry.Generation, (uint)i));
				yield return (id, entry.Value);
			}
		}

		// Return assets from hash storage
		foreach (var kvp in HashStorage) {
			yield return (new AssetId<T>(kvp.Key), kvp.Value);
		}
	}

	/// <summary>
	/// Returns an iterator over the <see cref="AssetId{T}"/> and mutable <see cref="IAsset"/> ref of every asset in this collection.
	/// </summary>
	/// <remarks>This will emit <see cref="AssetEventType.Modified"/> events for all assets iterated over.</remarks>
	public IEnumerable<(AssetId<T> Id, T Asset)> IterMut()
	{
		var modifiedIds = new List<AssetId<T>>();

		// Return assets from dense storage
		for (int i = 0; i < DenseStorage.storage.Count; i++) {
			var entry = DenseStorage.storage[i];
			if (entry.Value != null) {
				var id = new AssetId<T>(new AssetIndex(entry.Generation, (uint)i));
				modifiedIds.Add(id);
				yield return (id, entry.Value);
			}
		}

		// Return assets from hash storage
		foreach (var kvp in HashStorage) {
			var id = new AssetId<T>(kvp.Key);
			modifiedIds.Add(id);
			yield return (id, kvp.Value);
		}

		// Queue modified events for all accessed assets
		foreach (var id in modifiedIds) {
			QueuedEvents.Add(new AssetEvent<T> { Type = AssetEventType.Modified, Id = id });
		}
	}

	/// <summary>
	/// A system for tracking asset drops for the asset server and this asset collection
	/// </summary>
	/// <param name="server"></param>
	[Schedule(Schedules.PreUpdate)]
	[InSet<AssetSystems>(AssetSystems.TrackAssetSystems)]
	public void TrackAssets(Res<AssetServer> server)
	{
		server.Value.ProcessAssetDrops<T>(this);
	}

	[Schedule(Schedules.PostUpdate)]
	[InSet<AssetSystems>(AssetSystems.AssetEventSystems)]
	public void AssetEvents()
	{
		// TODO bevy syncs these to Messages<AssetEvents<T>> and ResMut<AssetChanges<T>>
		QueuedEvents.Clear();
	}
}

public enum AssetSystems
{
	/// <summary>
	/// A system set that holds all "track asset" operations.
	/// </summary>
	TrackAssetSystems,
	/// <summary>
	/// A system set where events accumulated in <see cref="Assets{T}"/> are applied to the [`AssetEvent`] [`Messages`] resource.
	/// </summary>
	AssetEventSystems,
}