namespace Verse.Assets;

public interface IAsset : IVisitAssetDependencies { }

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

	private List<Entry> storage;
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
		var entry = storage[(int)index.Index];
		if (entry.Generation != index.Generation) throw new InvalidOperationException("Invalid generation for asset index");
		var hadValue = entry.Value != null;
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
		if (actualIndex >= length) throw new IndexOutOfRangeException();
		var entry = storage[actualIndex];
		if (entry.Generation != index.Generation) throw new InvalidOperationException("Invalid generation for asset index");
		return storage[actualIndex].Value;
	}

	internal void Flush()
	{
		var newLength = allocator.nextIndex;
		if (newLength > storage.Count) {
			for (var i = storage.Count; i < newLength; i++) {
				storage.Add(new Entry());
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
public sealed class Assets<T> where T : IAsset
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
	
	public AssetHandleProvider GetAssetHandleProvider() => HandleProvider;
	
}

