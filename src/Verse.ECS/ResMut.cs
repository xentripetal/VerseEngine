using Verse.ECS.Systems;

namespace Verse.ECS;

public class OptionalResMut<T> : ISystemParam, IFromWorld<OptionalResMut<T>>
{
	internal OptionalResMut(ComponentId id)
	{
		componentId = id;
	}
	private ComponentId componentId;
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredWrite(componentId);
	}
	
	private ResourceData? currentRecord;
	
	public bool HasValue => currentRecord is { IsPresent: true} ;

	public bool TryGetValue(out T value)
	{
		if (currentRecord is { IsPresent: true}) {
			value = currentRecord.Get<T>();
			return true;
		}
		value = default;
		return false;
	}
	
	public ref T? MutValue {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			Ticks.Changed = Ticks.ThisRun;
			if (currentRecord is null) return ref Unsafe.NullRef<T>();
			return ref currentRecord.GetRef<T>()!;
		}
	}

	public T? Value {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => currentRecord.Get<T>();
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set {
			currentRecord.GetRef<T>() = value;
			Ticks.Changed = Ticks.ThisRun;
		}
	}	
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		currentRecord = world.Resources.Get(componentId);
		if (currentRecord is null) return;
		var resourceTicks = currentRecord.GetTicks()!;
		Ticks = new Ticks {
			Added = resourceTicks.Value.Added,
			Changed = resourceTicks.Value.Changed,
			ThisRun = thisRun,
			LastRun = meta.Ticks.LastRun,
		};
	}
	
	/// <summary>
	/// The tick state of the resource
	/// </summary>
	public Ticks Ticks;

	public static OptionalResMut<T> FromWorld(World world) => new OptionalResMut<T>(world.RegisterResource<T>());
}

public class ResMut<T> : ISystemParam, IFromWorld<ResMut<T>>
{
	internal ResMut(ComponentId id)
	{
		componentId = id;
	}

	/// <summary>
	/// The component ID of the resource
	/// </summary>
	private ComponentId componentId;

	/// <summary>
	/// The current resource entry. This can only be used for the lifecycle of a single system execution as the entry
	/// might move or be invalidated.
	/// </summary>
	private ResourceData currentRecord;

	/// <summary>
	/// The tick state of the resource
	/// </summary>
	public Ticks Ticks;

	public ref T MutBypassChangeDetection => ref currentRecord.GetRef<T>();

	public ref T MutValue {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			Ticks.Changed = Ticks.ThisRun;
			return ref currentRecord.GetRef<T>();
		}
	}

	public T Value {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => currentRecord.Get<T>();
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set {
			currentRecord.GetRef<T>() = value;
			Ticks.Changed = Ticks.ThisRun;
		}
	}

	public void MarkChanged()
	{
		Ticks.Changed = Ticks.ThisRun;
	}

	public static implicit operator T(ResMut<T> res) => res.Value;

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredWrite(componentId);
	}

	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		currentRecord = world.Resources.Get(componentId);
		if (currentRecord is not { IsPresent: true })
			throw new InvalidOperationException($"Resource of type {typeof(T)} does not exist.");

		var resourceTicks = currentRecord.GetTicks()!;
		Ticks = new Ticks {
			Added = resourceTicks.Value.Added,
			Changed = resourceTicks.Value.Changed,
			ThisRun = thisRun,
			LastRun = meta.Ticks.LastRun,
		};
	}

	public static ResMut<T> FromWorld(World world) => new ResMut<T>(world.RegisterResource<T>());
}

public class OptionalRes<T> : ISystemParam, IFromWorld<OptionalRes<T>>
{
	internal OptionalRes(ComponentId id)
	{
		componentId = id;
	}
	private ComponentId componentId;
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(componentId);
	}
	
	private ResourceData? currentRecord;
	
	public bool HasValue => currentRecord is { IsPresent: true} ;

	public bool TryGetValue(out T value)
	{
		if (currentRecord is { IsPresent: true}) {
			value = currentRecord.Get<T>();
			return true;
		}
		value = default;
		return false;
	}
	

	public T? Value {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => currentRecord.Get<T>();
	}	
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		currentRecord = world.Resources.Get(componentId);
		if (currentRecord is null) return;
		var resourceTicks = currentRecord.GetTicks()!;
		Ticks = new Ticks {
			Added = resourceTicks.Value.Added,
			Changed = resourceTicks.Value.Changed,
			ThisRun = thisRun,
			LastRun = meta.Ticks.LastRun,
		};
	}
	
	/// <summary>
	/// The tick state of the resource
	/// </summary>
	public Ticks Ticks;

	public static OptionalResMut<T> FromWorld(World world) => new OptionalResMut<T>(world.RegisterResource<T>());
}

public class Res<T> : ISystemParam, IFromWorld<Res<T>>
{
	internal Res(ComponentId id)
	{
		componentId = id;
	}

	/// <summary>
	/// The component ID of the resource
	/// </summary>
	private ComponentId componentId;
	
	/// <summary>
	/// The tick state of the resource
	/// </summary>
	public Ticks Ticks;
	
	private ResourceData currentRecord;

	public ref T Value {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref currentRecord.GetRef<T>();
	}

	public static implicit operator T(Res<T> res) => res.Value;

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(componentId);
	}

	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		currentRecord = world.Resources.Get(componentId);
		if (currentRecord is not { IsPresent: true })
			throw new InvalidOperationException($"Resource of type {typeof(T)} does not exist.");
		var resourceTicks = currentRecord.GetTicks()!;
		Ticks = new Ticks {
			Added = resourceTicks.Value.Added,
			Changed = resourceTicks.Value.Changed,
			ThisRun = thisRun,
			LastRun = meta.Ticks.LastRun,
		};
	}

	public Res<T> GetParam(SystemMeta meta, World world, Tick changeTick)
	{
		return this;
	}

	public static Res<T> FromWorld(World world) => new Res<T>(world.RegisterResource<T>());
}