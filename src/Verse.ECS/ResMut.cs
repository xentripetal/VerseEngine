using Verse.ECS.Systems;

namespace Verse.ECS;

public class ResMut<T> : ISystemParam, IFromWorld<ResMut<T>>
	where T : class
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
	private ResourceData? currentRecord = null;

	/// <summary>
	/// The tick state of the resource
	/// </summary>
	public Ticks Ticks;

	public T DirectValue;

	public T Value {
		get => DirectValue;
		set {
			DirectValue = value;
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
		DirectValue = currentRecord.Get<T>();	
		
	}

	public static ResMut<T> FromWorld(World world) => new ResMut<T>(world.RegisterResource<T>());
}

public class Res<T> : ISystemParam, IFromWorld<Res<T>>
	where T : class
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

	public T Value;

	public static implicit operator T(Res<T> res) => res.Value;

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(componentId);
	}

	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		var record = world.Resources.Get(componentId);
		if (record is not { IsPresent: true })
			throw new InvalidOperationException($"Resource of type {typeof(T)} does not exist.");
		var resourceTicks = record.GetTicks()!;
		Ticks = new Ticks {
			Added = resourceTicks.Value.Added,
			Changed = resourceTicks.Value.Changed,
			ThisRun = thisRun,
			LastRun = meta.Ticks.LastRun,
		};
		Value = record.Get<T>();
	}

	public Res<T> GetParam(SystemMeta meta, World world, Tick changeTick)
	{
		return this;
	}

	public static Res<T> FromWorld(World world) => new Res<T>(world.RegisterResource<T>());
}