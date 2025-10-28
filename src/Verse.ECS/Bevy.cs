using System.Diagnostics.CodeAnalysis;
using Verse.ECS.Systems;

namespace Verse.ECS;

public interface IEventParam
{
	void Clear();
}

public class EventRegistry()
{
	protected List<IEventParam> _eventParams = new List<IEventParam>();
	public void Clear()
	{
		_eventParams.Clear();
	}

	/// <summary>
	/// Updates all of the registered events in the world
	/// </summary>
	/// <param name="world"></param>
	/// <param name="tick"></param>
	public void Update(World world, uint tick)
	{
		foreach (var ev in _eventParams) {
			ev.Clear();
		}
	}

	internal void Register<T>(Messages<T> ev) where T : notnull
	{
		_eventParams.Add(ev);
	}
}

public sealed class Messages<T> : IEventParam, IFromWorld<Messages<T>>
	where T : notnull
{
	private readonly List<T> eventsLastFrame = new List<T>();
	private readonly List<T> eventsThisFrame = new List<T>();

	internal Messages()
	{
		// todo refactor to use a offset id in cursors
		Writer = new EventWriter<T>(eventsThisFrame);
		Reader = new EventReader<T>(eventsLastFrame);
	}

	public EventWriter<T> Writer { get; }
	public EventReader<T> Reader { get; }


	public void Clear()
	{
		eventsLastFrame.Clear();
		eventsLastFrame.AddRange(eventsThisFrame);
		eventsThisFrame.Clear();
	}

	public static Messages<T> FromWorld(World world)
	{
		var existing = world.GetResource<Messages<T>>();
		if (existing != null)
			return existing;
		var messages = new Messages<T>();
		world.InsertResource(messages);
		world.EventRegistry.Register(messages);
		return messages;
	}
}

public sealed class EventWriter<T> : ISystemParam, IFromWorld<EventWriter<T>>
	where T : notnull
{
	private readonly List<T> _events;

	internal EventWriter(List<T> events)
	{
		_events = events;
	}

	public bool IsEmpty
		=> _events.Count == 0;

	public void Clear()
		=> _events.Clear();

	public void Enqueue(T ev)
		=> _events.Add(ev);

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredWrite(world.ResourceId<Messages<T>>()!.Value);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }

	public static EventWriter<T> FromWorld(World world)
	{
		return world.Resource<Messages<T>>().Writer;
	}
}

public sealed class EventReader<T> : ISystemParam, IFromWorld<EventReader<T>>
	where T : notnull
{
	private readonly List<T> _events;

	internal EventReader(List<T> queue)
	{
		_events = queue;
	}

	public bool IsEmpty
		=> _events.Count == 0;

	public IEnumerable<T> Values => _events;

	public void Clear()
		=> _events.Clear();

	public List<T>.Enumerator GetEnumerator()
		=> _events.GetEnumerator();
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(world.ResourceId<Messages<T>>()!.Value);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static EventReader<T> FromWorld(World world)
	{
		return world.Resource<Messages<T>>().Reader;
	}
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

public class Query<TQueryData> : Query<TQueryData, Empty>, ISystemParam, IFromWorld<Query<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Query(Query query) : base(query) { }

	public static Query<TQueryData> FromWorld(World world)
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
	private readonly Query _query;

	internal Query(Query query)
	{
		_query = query;
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
		EcsAssert.Panic(_query.Count() == 1, "'Single' must match one and only one entity.");
		var enumerator = GetEnumerator();
		var ok = enumerator.MoveNext();
		EcsAssert.Panic(ok, "'Single' is not matching any entity.");
		return enumerator.Current;
	}

	public int Count()
		=> _query.Count();

	private ISystem _system;
	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) =>
		new QueryIter<TQueryData, TQueryFilter>(_system.Meta.Ticks.LastRun, thisRun,
			id == 0 ? _query.Iter(thisRun) : _query.Iter(id, thisRun));
	public void Init(ISystem system, World world)
	{
		_system = system;
		_system.Meta.Access.Add(_query.BuildAccess());
	}

	private Tick thisRun;
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		this.thisRun = thisRun;
	}
	public bool Prepare(ISystem system, World world) => true;
	public static Query<TQueryData, TQueryFilter> FromWorld(World world)
	{
		var builder = world.QueryBuilder();
		TQueryData.Build(builder);
		TQueryFilter.Build(builder);
		return new Query<TQueryData, TQueryFilter>(builder.Build());
	}
}

public class Single<TQueryData> : Single<TQueryData, Empty>, ISystemParam, IFromWorld<Single<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Single(Query query) : base(query) { }

	public static Single<TQueryData> FromWorld(World world)
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
	private readonly Query _query;

	internal Single(Query query)
	{
		_query = query;
	}

	public TQueryData Get()
	{
		EcsAssert.Panic(_query.Count() == 1, "'Single' must match one and only one entity.");
		var enumerator = GetIter();
		var ok = enumerator.MoveNext();
		EcsAssert.Panic(ok, "'Single' is not matching any entity.");
		return enumerator.Current;
	}

	public bool TryGet(out TQueryData data)
	{
		if (_query.Count() == 1) {
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
		=> _query.Count();

	private ISystem _system;
	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) =>
		new QueryIter<TQueryData, TQueryFilter>(_system.Meta.Ticks.LastRun, thisRun,
			id == 0 ? _query.Iter(thisRun) : _query.Iter(id, thisRun));
	public void Init(ISystem system, World world)
	{
		this._system = system;
		this._system.Meta.Access.Add(_query.BuildAccess());
	}

	private Tick thisRun;
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		this.thisRun = thisRun;
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
{
	private T? _t;
	public ref T? Value => ref _t;
	public static implicit operator T?(Local<T> reference)
		=> reference.Value;

	public void Init(ISystem system, World world) { }
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static Local<T> FromWorld(World world) => new Local<T>();
}

public sealed class Commands : ISystemParam, IFromWorld<Commands>
{
	private CommandBuffer _buffer;
	private readonly World _world;

	internal Commands(World world)
	{
		_world = world;
	}

	public EntityCommand Entity(EcsID id = 0)
	{
		var ent = _world.Entity(id);
		return new EntityCommand(_buffer, ent.Id);
	}
	
	public void InsetResource<T>(T resource) where T : class
	{
		_buffer.InsertResource(resource);
	}
	
	public void Init(ISystem system, World world)
	{
		_buffer = system.Buffer;
		system.Meta.HasDeferred = true;
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun) { }
	public static Commands FromWorld(World world) => new Commands(world);
}

public readonly ref struct EntityCommand
{
	private readonly CommandBuffer buffer;

	internal EntityCommand(CommandBuffer buffer, EcsID id)
	{
		(this.buffer, ID) = (buffer, id);
	}

	public readonly EcsID ID;

	public EntityCommand Set<T>(T component) where T : struct
	{
		buffer.Set(ID, component);
		return this;
	}

	public EntityCommand Unset<T>() where T : struct
	{
		buffer.Unset<T>(ID);
		return this;
	}

	public EntityCommand Delete()
	{
		buffer.Delete(ID);
		return this;
	}
}