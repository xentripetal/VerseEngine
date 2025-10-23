using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Verse.Assets;

/// <summary>
/// Provides <see cref="Handle{T}"/> and <see cref="UntypedHandle"/> for a specific asset type.
/// </summary>
public sealed class AssetHandleProvider
{
	internal AssetIndexAllocator Allocator;
	internal ChannelWriter<DropEvent> DropWriter;
	internal ChannelReader<DropEvent> DropReader;
	internal Type Type;

	public AssetHandleProvider(Type type, AssetIndexAllocator allocator)
	{
		var channel = Channel.CreateUnbounded<DropEvent>();
		DropWriter = channel.Writer;
		DropReader = channel.Reader;
		Type = type;
		Allocator = allocator;
	}

	public UntypedHandle ReserveHandle()
	{
		var index = Allocator.Reserve();
		return new UntypedHandle(GetHandle(new AssetIndexOrGuid(index), false, null, null));
	}

	public StrongHandle GetHandle(AssetIndexOrGuid id, bool assetServerManaged, AssetPath? path, Action<IAssetMeta>? metaTransform)
	{
		return new StrongHandle(id.Untyped(Type), assetServerManaged, path, DropWriter, metaTransform);
	}

	internal StrongHandle ReserveHandleInternal(bool assetServerManaged, AssetPath? path, Action<IAssetMeta>? metaTransform)
	{
		var index = Allocator.Reserve();
		return GetHandle(new AssetIndexOrGuid(index), assetServerManaged, path, metaTransform);
	}
}

/// <summary>
/// StrongHandleOrGuid is a container for either a <see cref="StrongHandle"/> or a <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// This is used in <see cref="Handle{T}"/> and <see cref="UntypedHandle"/> for an explicit layout type since you cannot
/// use StructLayouts on generic types
/// </remarks>
/// <remarks>
/// Doesn't actually use struct layouts for emmory optization since you cant mix reference types with structs for now.
/// Waiting on DU support in C#15
/// </remarks>
public record struct StrongHandleOrGuid : IComparable<StrongHandleOrGuid>
{
	private struct HandleHolder
	{
		public StrongHandle Handle;
	}

	private readonly Guid _guid;
	private readonly HandleHolder _handle;
	private readonly ulong _padding;

	public StrongHandleOrGuid(Guid guid)
	{
		_guid = guid;
	}

	public StrongHandleOrGuid(StrongHandle handle)
	{
		_padding = 0;
		_handle = new HandleHolder {
			Handle = handle
		};
	}

	public bool IsGuid => _padding != 0;
	public StrongHandle AsHandle => IsGuid ? throw new InvalidOperationException("StrongHandleOrGuid contains a Guid, not a StrongHandle") : _handle.Handle;
	public Guid AsGuid => IsGuid ? _guid : throw new InvalidOperationException("StrongHandleOrGuid contains a StrongHandle, not a Guid");

	public int CompareTo(StrongHandleOrGuid other) => _guid.CompareTo(other._guid);
	public bool Equals(StrongHandleOrGuid other) => _guid.Equals(other._guid);

	public override int GetHashCode()
	{
		return _guid.GetHashCode();
	}
}

/// <summary>
/// A handle to a specific <see cref="IAsset"/> of type <typeparamref name="T"/>. Handles act as an abstract reference
/// to assets, whose data are stored in the <see cref="Assets"/> resource, avoiding the need to store multiple copies
/// of the same data
/// </summary>
/// <remarks>
/// <para>
/// If a Handle is a <see cref="StrongHandle"/>, the <see cref="IAsset"/> will be kept alive until the <see cref="Handle{T}"/>
/// is garbage collected. If a Handle is a <see cref="Guid"/>, it does not necessarily reference an alive <see cref="IAsset"/>,
/// nor will it keep assets alive.
/// </para>
/// </remarks>
/// <typeparam name="T"></typeparam>
public struct Handle<T> : IVisitAssetDependencies
	where T : IAsset
{
	public Handle(StrongHandle handle)
	{
		_handle = new StrongHandleOrGuid(handle);
	}

	public Handle(Guid guid)
	{
		_handle = new StrongHandleOrGuid(guid);
	}

	public Handle(StrongHandleOrGuid handle)
	{
		_handle = handle;
	}

	private StrongHandleOrGuid _handle;

	public void VisitDependencies(Action<UntypedAssetId> visit)
	{
		visit(Id().Untyped());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public AssetId<T> Id()
	{
		return _handle.IsGuid ? new AssetId<T>(_handle.AsGuid) : _handle.AsHandle.Id.TypedUnchecked<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public AssetPath? Path()
	{
		return IsGuid() ? null : _handle.AsHandle.Path;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsGuid() => _handle.IsGuid;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsStrong() => !_handle.IsGuid;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UntypedHandle Untyped()
	{
		if (IsGuid()) {
			return new UntypedHandle(typeof(T), _handle.AsGuid);
		}
		return new UntypedHandle(_handle.AsHandle);
	}
}

/// <summary>
/// An untyped variant of a <see cref="Handle{T}"/>, which internally stores the <see cref="IAsset"/> type information
/// at runtime instead of encoding it in the compile-time type. This allows handles across <see cref="IAsset"/> types
/// to be stored teogether and compared
/// </summary>
/// <seealso cref="Handle{T}"/>
public struct UntypedHandle : IIntoUntypedAssetId
{
	public UntypedHandle(Type type, Guid guid)
	{
		_handle = new StrongHandleOrGuid(guid);
		Type = type;
	}

	public UntypedHandle(StrongHandle handle)
	{
		_handle = new StrongHandleOrGuid(handle);
		Type = handle.Id.Type;
	}
	private readonly StrongHandleOrGuid _handle;
	public readonly Type Type;

	public readonly UntypedAssetId Id()
	{
		return _handle.IsGuid ? new UntypedAssetId(Type, _handle.AsGuid) : _handle.AsHandle.Id;
	}

	public bool IsStrong()
	{
		return !_handle.IsGuid;
	}

	public bool IsGuid()
	{
		return _handle.IsGuid;
	}

	public Handle<T> Typed<T>() where T : IAsset
	{
		return new Handle<T>(_handle);
	}
	public UntypedAssetId IntoUntypedAssetId() => Id();
}

public record struct DropEvent(AssetIndexOrGuid AssetId, bool IsAssetServerManaged);

/// <summary>
/// The internal <see cref="IAsset"/> handle storage. When this is garbage collected, the <see cref="IAsset"/> will be freed.
/// It also stores some asset metadata for easy access from handles.
/// </summary>
public class StrongHandle : IIntoUntypedAssetId
{
	internal StrongHandle(UntypedAssetId id, bool assetServerManaged, AssetPath? path, ChannelWriter<DropEvent> onDrop, Action<IAssetMeta>? metaTransform)
	{
		Id = id;
		AssetServerManaged = assetServerManaged;
		Path = path;
		OnDrop = onDrop;
		MetaTransform = metaTransform;
	}

	internal UntypedAssetId Id;
	internal bool AssetServerManaged;
	// TODO maybe add a custom type for handling storage source
	internal AssetPath? Path;
	public ChannelWriter<DropEvent> OnDrop;
	/// <summary>
	/// Modifies asset meta during a load
	/// </summary>
	public Action<IAssetMeta>? MetaTransform;

	~StrongHandle()
	{
		if (!OnDrop.TryWrite(new DropEvent(Id.Id, AssetServerManaged))) {
			throw new InvalidOperationException("Failed to write drop event");
		}
	}
	public UntypedAssetId IntoUntypedAssetId() => Id;
}