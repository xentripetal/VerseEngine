namespace Verse.ECS;

/// <summary>
///     A wrapper around an EcsID which contains shortcuts methods.
/// </summary>
[SkipLocalsInit]
[DebuggerDisplay("ID: {Id}")]
public readonly struct EntityView : IEquatable<EcsID>, IEquatable<EntityView>, IComparable<EntityView>, IComparable<EcsID>
{
	public static readonly EntityView Invalid = new EntityView(null!, 0);


	/// <inheritdoc cref="EcsID" />
	public readonly EcsID Id;

	/// <inheritdoc cref="ECS.World" />
	public readonly World World;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal EntityView(World world, EcsID id)
	{
		World = world;
		Id = id;
	}

	/// <inheritdoc cref="EcsID.Generation" />
	public readonly int Generation => Id.Generation();


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(EcsID other)
		=> Id == other;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(EntityView other)
		=> Id == other.Id;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly override int GetHashCode()
		=> Id.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly override bool Equals(object? obj)
		=> obj is EntityView ent && Equals(ent);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int CompareTo(EntityView ent)
		=> Id.CompareTo(ent.Id);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int CompareTo(EcsID ent)
		=> Id.CompareTo(ent);


	/// <inheritdoc cref="World.Add{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Add<T>() where T : struct
	{
		World.Add<T>(Id);
		return this;
	}

	/// <inheritdoc cref="World.Set{T}(EcsID, T)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Set<T>(T component) where T : struct
	{
		World.Set(Id, component);
		return this;
	}

	/// <inheritdoc cref="World.Add(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Add(EcsID id)
	{
		World.Add(Id, id);
		return this;
	}

	/// <inheritdoc cref="World.Add(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Add(EntityView id)
		=> Add(id.Id);

	/// <inheritdoc cref="World.Unset{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Unset<T>() where T : struct
	{
		World.Unset<T>(Id);
		return this;
	}

	/// <inheritdoc cref="World.Unset(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Unset(EcsID id)
	{
		World.Unset(Id, id);
		return this;
	}
	
	public readonly ROEntityView AsReadOnly() => new ROEntityView(World, Id);

	/// <inheritdoc cref="World.Unset(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly EntityView Unset(EntityView id)
		=> Unset(id.Id);

	/// <inheritdoc cref="World.GetSlimType" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<SlimComponent> SlimType()
		=> World.GetSlimType(Id);
	
	/// <inheritdoc cref="World.GetType" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<Component> Type()
		=> World.GetType(Id);

	/// <inheritdoc cref="World.Get{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref T Get<T>() where T : struct
		=> ref World.Get<T>(Id);

	/// <inheritdoc cref="World.Has{T}(EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has<T>() where T : struct
		=> World.Has<T>(Id);

	/// <inheritdoc cref="World.Has(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has(EcsID id)
		=> World.Has(Id, id);

	/// <inheritdoc cref="World.Has(EcsID, EcsID)" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Has(EntityView id)
		=> World.Has(Id, id.Id);

	/// <inheritdoc cref="World.Delete" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void Delete()
		=> World.Delete(Id);

	/// <inheritdoc cref="World.Exists" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Exists()
		=> World.Exists(Id);

	/// <inheritdoc cref="World.SetChanged" />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void SetChanged<T>() where T : struct
		=> World.SetChanged<T>(Id);

	public static implicit operator EcsID(EntityView d) => d.Id;

	public static bool operator ==(EntityView a, EntityView b) => a.Id.Equals(b.Id);
	public static bool operator !=(EntityView a, EntityView b) => !(a == b);
}