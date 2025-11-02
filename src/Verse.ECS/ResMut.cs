using Verse.ECS.Systems;

namespace Verse.ECS;

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

	public static ResMut<T> FromWorld(World world) => new ResMut<T>(world.RegisterResource<T>());
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