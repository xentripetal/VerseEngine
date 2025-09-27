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

	public StrongHandle GetHandle(AssetIndexOrGuid id, bool assetServerManaged, string? path, Action<IAssetMeta>? metaTransform)
	{
		return new StrongHandle(id.Untyped(Type), assetServerManaged, path, DropWriter, metaTransform);
	}

	internal StrongHandle ReserveHandleInternal(bool assetServerManaged, string? path, Action<IAssetMeta>? metaTransform)
	{
		var index = Allocator.Reserve();
		return GetHandle(new AssetIndexOrGuid(index), assetServerManaged, path, metaTransform);
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
[StructLayout(layoutKind: LayoutKind.Explicit)]
public struct Handle<T> : IVisitAssetDependencies
	where T : IAsset
{
	public Handle(StrongHandle handle)
	{
		_padding = 0;
		_strongHandle = handle;
	}

	public Handle(Guid guid)
	{
		_guid = guid;
	}


	[FieldOffset(0)] private readonly Guid _guid;
	[FieldOffset(0)] private readonly StrongHandle _strongHandle;
	[FieldOffset(8)] private readonly ulong _padding;

	public void VisitDependencies(Action<UntypedAssetId> visit)
	{
		visit(Id().Untyped());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public AssetId<T> Id()
	{
		if (IsGuid()) {
			return new AssetId<T>(_guid);
		}
		return _strongHandle.Id.TypedUnchecked<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string? Path()
	{
		if (IsGuid()) {
			return null;
		}
		return _strongHandle.Path;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsGuid() => _padding != 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsStrong() => _padding == 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UntypedHandle Untyped()
	{
		if (IsGuid()) {
			return new UntypedHandle(typeof(T), _guid);
		}
		return new UntypedHandle(_strongHandle);
	}
}

/// <summary>
/// An untyped variant of a <see cref="Handle{T}"/>, which internally stores the <see cref="IAsset"/> type information
/// at runtime instead of encoding it in the compile-time type. This allows handles across <see cref="IAsset"/> types
/// to be stored teogether and compared
/// </summary>
/// <seealso cref="Handle{T}"/>
[StructLayout(LayoutKind.Explicit)]
public struct UntypedHandle
{
	public UntypedHandle(Type type, Guid guid)
	{
		_guid = guid;
		_padding = 0;
		Type = type;
	}

	public UntypedHandle(StrongHandle handle)
	{
		_padding = 0;
		_strongHandle = handle;
		Type = handle.Id.Type;
	}
	[FieldOffset(0)] private readonly Guid _guid;
	[FieldOffset(0)] private readonly StrongHandle _strongHandle; // stored as a pointer, assume x64
	[FieldOffset(8)] private ulong _padding;
	[FieldOffset(16)] public readonly Type Type;

	public UntypedAssetId Id()
	{
		if (_padding == 0) {
			return _strongHandle.Id;
		}
		return new UntypedAssetId(Type, _guid);
	}

	public bool IsStrong()
	{
		return _padding == 0;
	}

	public bool IsGuid()
	{
		return _padding != 0;
	}
}

public record struct DropEvent(AssetIndexOrGuid AssetId, bool IsAssetServerManaged);

/// <summary>
/// The internal <see cref="IAsset"/> handle storage. When this is garbage collected, the <see cref="IAsset"/> will be freed.
/// It also stores some asset metadata for easy access from handles.
/// </summary>
public class StrongHandle
{
	internal StrongHandle(UntypedAssetId id, bool assetServerManaged, string? path, ChannelWriter<DropEvent> onDrop, Action<IAssetMeta>? metaTransform)
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
	internal string? Path;
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
}