using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Verse.Assets;


/// <summary>
/// A unique identifier for an <see cref="IAsset"/>. This is cheap to use and is not directly tied to the lifetime of the
/// asset. This means it can point to a <see cref="IAsset"/> that no longer exists.
/// </summary>
/// <seealso cref="Handle{T}">Handle: for an identifier tied to the lifetime of an asset</seealso>
/// <seealso cref="UntypedAssetId">UntypedAssetId: for an untyped id</seealso>
public record struct AssetId<T>(AssetIndexOrGuid Id) where T : IAsset
{
	public AssetId(AssetIndex index) : this(new AssetIndexOrGuid(index)) { }
	public AssetId(Guid guid) : this(new AssetIndexOrGuid(guid)) { }
	/// <summary>
	/// The UUID for the default <see cref="AssetId{T}"/>. It is valid to assign a value to this in <see cref="Assets"/>
	/// and by convention (where appropriate) assets should support this pattern..
	/// </summary>
	/// <remarks>Value taken from Bevy, u128 of 200809721996911295814598172825939264631</remarks>
	public static Guid DefaultGuid = new Guid(new byte[] { 0x97, 0x12, 0x8b, 0xb1, 0x25, 0x88, 0x48, 0x0b, 0xbd, 0xc6, 0x87, 0xb4, 0xad, 0xbe, 0xc4, 0x77 });
	/// <summary>
	/// This asset id should never be valid. Assigning a value to this in <see cref="Assets"/> will produce undefined
	/// behavior, so don't do it!
	/// </summary>
	/// <remarks>Value taken from bevy, u128 of 108428345662029828789348721013522787528</remarks>
	public static Guid InvalidGuid = new Guid(new byte[] { 0x51, 0x92, 0x8a, 0x2e, 0x91, 0xad, 0x41, 0x3c, 0xa9, 0x10, 0xd0, 0x2c, 0x6a, 0x71, 0x6c, 0xc8 });

	public bool Invalid()
	{
		return Id.IsGuid && Id.AsGuid == InvalidGuid;
	}

	public UntypedAssetId Untyped()
	{
		return new UntypedAssetId(typeof(T), Id);
	}
}

public sealed class AssetIndexAllocator
{
	internal uint nextIndex;
	private ChannelWriter<AssetIndex> recycledQueueSender;
	/// <summary>
	/// This receives every recycled <see cref="AssetIndex"/>. It serves as a buffer/queue to store indices ready for reuse
	/// </summary>
	private ChannelReader<AssetIndex> recycledQueueReceiver;
	public ChannelWriter<AssetIndex> recycledSender;
	internal ChannelReader<AssetIndex> RecycledReceiver;

	public AssetIndexAllocator()
	{
		var recycledQueue = Channel.CreateUnbounded<AssetIndex>();
		var recycled = Channel.CreateUnbounded<AssetIndex>();
		recycledQueueSender = recycledQueue.Writer;
		recycledQueueReceiver = recycledQueue.Reader;
		recycledSender = recycled.Writer;
		RecycledReceiver = recycled.Reader;
		nextIndex = 0;
	}

	/// <summary>
	/// Reserves a new <see cref="AssetIndex"/>, either by reusing a recycled index (with an incremented generation), or by
	/// creating a new index by incrementing the index counter for a given asset type.
	/// </summary>
	/// <returns></returns>
	public AssetIndex Reserve()
	{
		if (recycledQueueReceiver.TryRead(out var recycled)) {
			recycled.Generation++;
			// Tell storage that it can wipe the value
			if (!recycledSender.TryWrite(recycled)) {
				// It is unbounded, this should not happen
				throw new InvalidOperationException("Failed to write recycled index");
			}
			return recycled;
		}
		var index = Interlocked.Increment(ref nextIndex);
		// We start at generation 1 since storage will treat gen 0 as invalid
		return new AssetIndex(1, index-1);
	}

	public void Recycle(AssetIndex index)
	{
		if (!recycledQueueSender.TryWrite(index)) {
			throw new InvalidOperationException("Failed to write recycled index");
		}
	}
	
}



/// <summary>
/// A generational runtime-only identifier for a specific <see cref="Asset"/> stored in <see cref="Assets"/>. This is
/// optimized for efficient runtime usage and is not suitable for identifying assets across app runs.
/// </summary>
public record struct AssetIndex(uint Generation, uint Index);

[StructLayout(LayoutKind.Explicit)]
public record struct AssetIndexOrGuid : IComparable<AssetIndexOrGuid>
{
	[FieldOffset(0)] private readonly Guid _guid;
	[FieldOffset(0)] private readonly AssetIndex _index;
	[FieldOffset(8)] private readonly ulong _upperBytes;

	public AssetIndexOrGuid(Guid guid)
	{
		_guid = guid;
	}

	public AssetIndexOrGuid(AssetIndex index)
	{
		_upperBytes = 0;
		_index = index;
	}

	public bool IsGuid => _upperBytes != 0;
	public AssetIndex AsIndex => IsGuid ? throw new InvalidOperationException("AssetIndexOrGuid contains a Guid, not an AssetIndex") : _index;
	public Guid AsGuid => IsGuid ? _guid : throw new InvalidOperationException("AssetIndexOrGuid contains an AssetIndex, not a Guid");

	public int CompareTo(AssetIndexOrGuid other) => _guid.CompareTo(other._guid);
	public bool Equals(AssetIndexOrGuid other) => _guid.Equals(other._guid);

	public override int GetHashCode()
	{
		return _guid.GetHashCode();
	}

	public AssetId<T> Typed<T>() where T : IAsset
	{
		return new AssetId<T>(this);
	}

	public UntypedAssetId Untyped(Type type)
	{
		return new UntypedAssetId(type, this);
	}

}

/// <summary>
/// An untyped <see cref="IAsset"/> identifier that behaves much like <see cref="AssetId"/>, but stores the
/// <see cref="Asset"/> type information at runtime instead of compile-time. This increases the size of the type, but
/// it enables storing asset ids across asset types together and enables comparison between them.
/// 
/// </summary>
public record struct UntypedAssetId : IComparable<UntypedAssetId>
{
	public readonly AssetIndexOrGuid Id;
	public readonly Type Type;
	public UntypedAssetId(Type type, AssetIndex index)
	{
		Type = type;
		Id = new AssetIndexOrGuid(index);
	}
	public UntypedAssetId(Type type, Guid guid)
	{
		Type = type;
		Id = new AssetIndexOrGuid(guid);
	}
	public UntypedAssetId(Type type, AssetIndexOrGuid id)
	{
		Type = type;
		Id = id;
	}
	public bool IsGuid => Id.IsGuid;

	public AssetIndex AsIndex => Id.AsIndex;
	public Guid AsGuid => Id.AsGuid;

	public int CompareTo(UntypedAssetId other)
	{
		// First compare by type
		var typeComparison = Type.GetHashCode() - other.Type.GetHashCode();
		if (typeComparison != 0) return typeComparison;
		return Id.CompareTo(other.Id);
	}

	public bool Equals(UntypedAssetId other)
	{
		return Type == other.Type && Id.Equals(other.Id);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Type, Id);
	}
	
	public AssetId<T> TypedUnchecked<T> () where T : IAsset
	{
		return new AssetId<T>(Id);
	}
	public AssetId<T> Typed<T> () where T : IAsset
	{
		if (Type != typeof(T)) throw new InvalidOperationException($"AssetId is not of type {typeof(T)}");
		return new AssetId<T>(Id);
	}
}