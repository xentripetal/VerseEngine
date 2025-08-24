namespace Verse.ECS;

/// <summary>
///     A wrapper around an EcsID which contains shortcuts methods.
/// </summary>
[SkipLocalsInit]
[DebuggerDisplay("ID: {Id}")]
public readonly struct ROEntityView : IEquatable<EcsID>, IEquatable<ROEntityView>, IComparable<ROEntityView>, IComparable<EcsID>
{
	public static readonly ROEntityView Invalid = new ROEntityView(null!, 0);


	/// <inheritdoc cref="EcsID" />
	public readonly EcsID Id;

	/// <inheritdoc cref="ECS.World" />
	private readonly World _world;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ROEntityView(World world, EcsID id)
	{
		_world = world;
		Id = id;
	}

	/// <inheritdoc cref="EcsID.Generation" />
	public readonly int Generation => Id.Generation();


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(EcsID other)
		=> Id == other;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(ROEntityView other)
		=> Id == other.Id;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly override int GetHashCode()
		=> Id.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly override bool Equals(object? obj)
	{
		if (obj is EcsID id)
			return Equals(id);
		if (obj is ROEntityView id2)
			return Equals(id2);
		if (obj is EntityView id3)
			return Equals(id3);
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int CompareTo(ROEntityView ent)
		=> Id.CompareTo(ent.Id);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int CompareTo(EcsID ent)
		=> Id.CompareTo(ent);

	/// <inheritdoc cref="World.GetSlimType" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<SlimComponent> SlimType()
		=> _world.GetSlimType(Id);
	
	/// <inheritdoc cref="World.GetType" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<Component> Type()
		=> _world.GetType(Id);

	/// <inheritdoc cref="World.Get{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref T Get<T>() where T : struct
		=> ref _world.Get<T>(Id);

	/// <inheritdoc cref="World.Has{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has<T>() where T : struct
		=> _world.Has<T>(Id);

	/// <inheritdoc cref="World.Has(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has(EcsID id)
		=> _world.Has(Id, id);

	/// <inheritdoc cref="World.Has(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has(ROEntityView id)
		=> _world.Has(Id, id.Id);

	/// <inheritdoc cref="World.Exists" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Exists()
		=> _world.Exists(Id);

	public static implicit operator EcsID(ROEntityView d) => d.Id;

	public static bool operator ==(ROEntityView a, ROEntityView b) => a.Id.Equals(b.Id);
	public static bool operator !=(ROEntityView a, ROEntityView b) => !(a == b);
}