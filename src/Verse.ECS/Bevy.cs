using Verse.ECS.Systems;

namespace Verse.ECS;

public interface IEventParam
{
	void Clear();
}

partial class World : ISystemParam, IFromWorld<World>
{
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.WriteAll();
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static World FromWorld(World world) => world;
}

public class Query<TQueryData> : Query<TQueryData, Empty>, IFromWorld<Query<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Query(Query query) : base(query) { }

	public new static Query<TQueryData> FromWorld(World world)
	{
		var builder = world.QueryBuilder();
		TQueryData.Build(builder);
		return new Query<TQueryData>(builder.Build());
	}
}

public class Query<TQueryData, TQueryFilter> : ISystemParam, IFromWorld<Query<TQueryData, TQueryFilter>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
	where TQueryFilter : struct, IFilter<TQueryFilter>, allows ref struct
{
	private readonly Query query;
	private ISystem? system;

	internal Query(Query query)
	{
		this.query = query;
		system = null;
	}

	public QueryIter<TQueryData, TQueryFilter> GetEnumerator()
		=> GetIter();

	public TQueryData Get(EcsID id)
	{
		var enumerator = GetIter(id);
		var success = enumerator.MoveNext();
		return success ? enumerator.Current : default;
	}

	public bool Contains(EcsID id)
	{
		var enumerator = GetIter(id);
		return enumerator.MoveNext();
	}

	public TQueryData Single()
	{
		EcsAssert.Panic(query.Count() == 1, "'Single' must match one and only one entity.");
		var enumerator = GetEnumerator();
		var ok = enumerator.MoveNext();
		EcsAssert.Panic(ok, "'Single' is not matching any entity.");
		return enumerator.Current;
	}

	public int Count()
		=> query.Count();

	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) =>
		new QueryIter<TQueryData, TQueryFilter>(id == 0 ? query.Iter(system!.Meta.Ticks.LastRun, thisRun) : query.Iter(id, system!.Meta.Ticks.LastRun, thisRun));
	public void Init(ISystem sys, World world)
	{
		system = sys;
		system.Meta.Access.Add(query.BuildAccess());
	}

	private Tick thisRun;
	public void ValidateParam(SystemMeta meta, World world, Tick tick)
	{
		thisRun = tick;
	}
	public static Query<TQueryData, TQueryFilter> FromWorld(World world)
	{
		var builder = world.QueryBuilder();
		TQueryData.Build(builder);
		TQueryFilter.Build(builder);
		return new Query<TQueryData, TQueryFilter>(builder.Build());
	}
}

public class Single<TQueryData> : Single<TQueryData, Empty>, IFromWorld<Single<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Single(Query query) : base(query) { }

	public new static Single<TQueryData> FromWorld(World world)
	{
		var builder = world.QueryBuilder();
		TQueryData.Build(builder);
		return new Single<TQueryData>(builder.Build());
	}
}

public class Single<TQueryData, TQueryFilter> : ISystemParam, IFromWorld<Single<TQueryData, TQueryFilter>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
	where TQueryFilter : struct, IFilter<TQueryFilter>, allows ref struct
{
	private readonly Query query;
	private ISystem? system;
	private Tick thisRun;
	private Tick lastRun;
	
	internal Single(Query query)
	{
		this.query = query;
	}

	public TQueryData Get()
	{
		EcsAssert.Panic(query.Count() == 1, "'Single' must match one and only one entity.");
		var enumerator = GetIter();
		var ok = enumerator.MoveNext();
		EcsAssert.Panic(ok, "'Single' is not matching any entity.");
		return enumerator.Current;
	}

	public ROEntityView GetEntity()
	{
		return query.Iter(lastRun, thisRun).Entities()[0];
	}

	public bool TryGet(out TQueryData data)
	{
		if (query.Count() == 1) {
			var enumerator = GetIter();
			var ok = enumerator.MoveNext();
			if (ok) {
				data = enumerator.Current;
				return true;
			}
		}

		data = default;
		return false;
	}

	public int Count()
		=> query.Count();

	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) =>
		new QueryIter<TQueryData, TQueryFilter>(id == 0 ? query.Iter(lastRun, thisRun) : query.Iter(id, lastRun, thisRun));
	public void Init(ISystem sys, World world)
	{
		system = sys;
		system.Meta.Access.Add(query.BuildAccess());
	}

	
	public void ValidateParam(SystemMeta meta, World world, Tick tick)
	{
		thisRun = tick;
		lastRun = meta.Ticks.LastRun;
	}

	public static Single<TQueryData, TQueryFilter> FromWorld(World world)
	{
		var builder = world.QueryBuilder();
		TQueryData.Build(builder);
		TQueryFilter.Build(builder);
		return new Single<TQueryData, TQueryFilter>(builder.Build());
	}
}

public sealed class Local<T> : ISystemParam, IFromWorld<Local<T>>
	where T : new()
{
	private T t = new T();
	public ref T Value => ref t;
	public static implicit operator T(Local<T> reference)
		=> reference.Value;

	public void Init(ISystem system, World world) { }
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static Local<T> FromWorld(World world) => new Local<T>();
}

public sealed class Commands : ISystemParam, IFromWorld<Commands>
{
	private CommandBuffer? buffer;
	private readonly World world;

	internal Commands(World world)
	{
		this.world = world;
	}

	public EntityCommand Entity(EcsID id = 0)
	{
		var ent = world.Entity(id);
		return new EntityCommand(buffer!, ent.Id);
	}

	public void InsertResource<T>(T resource) 
	{
		buffer!.InsertResource(resource);
	}

	public void Init(ISystem system, World w)
	{
		buffer = system.Buffer;
		system.Meta.HasDeferred = true;
	}
	public void ValidateParam(SystemMeta meta, World w, Tick thisRun) { }
	public static Commands FromWorld(World world) => new Commands(world);
}

public readonly ref struct EntityCommand
{
	private readonly CommandBuffer buffer;

	internal EntityCommand(CommandBuffer buffer, EcsID id)
	{
		(this.buffer, Id) = (buffer, id);
	}

	public readonly EcsID Id;

	public EntityCommand Set<T>(T component) 
	{
		buffer.Set(Id, component);
		return this;
	}

	public EntityCommand Unset<T>() 
	{
		buffer.Unset<T>(Id);
		return this;
	}

	public EntityCommand Delete()
	{
		buffer.Delete(Id);
		return this;
	}
}