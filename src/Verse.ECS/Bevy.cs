using System.Diagnostics.CodeAnalysis;
using Verse.ECS.Systems;

namespace Verse.ECS;

// https://promethia-27.github.io/dependency_injection_like_bevy_from_scratch/introductions.html


/**
public partial class FuncSystem<TArg> where TArg : notnull
{
	private readonly LinkedList<FuncSystem<TArg>> _after = new LinkedList<FuncSystem<TArg>>();
	private readonly TArg _arg;
	private readonly LinkedList<FuncSystem<TArg>> _before = new LinkedList<FuncSystem<TArg>>();
	private readonly Func<bool> _checkInUse;
	private readonly List<Func<SystemTicks, TArg, bool>> _conditions;
	private readonly Func<SystemTicks, TArg, Func<SystemTicks, TArg, bool>, bool> _fn;
	private readonly Stages _stage;
	private readonly ThreadingMode _threadingType;
	private readonly Func<SystemTicks, TArg, bool> _validator;


	internal FuncSystem(TArg arg, Func<SystemTicks, TArg, Func<SystemTicks, TArg, bool>, bool> fn, Func<bool> checkInUse, Stages stage, ThreadingMode threadingType)
	{
		_arg = arg;
		_fn = fn;
		_conditions = new List<Func<SystemTicks, TArg, bool>>();
		_validator = ValidateConditions;
		_checkInUse = checkInUse;
		_threadingType = threadingType;
		_stage = stage;
	}
	internal LinkedListNode<FuncSystem<TArg>>? Node { get; set; }
	internal SystemTicks Ticks { get; } = new SystemTicks();

	internal void Run(uint ticks)
	{
		Ticks.ThisRun = ticks;

		foreach (var s in _before) {
			s.Run(ticks);
		}

		if (_fn(Ticks, _arg, _validator)) {
			foreach (var s in _after) {
				s.Run(ticks);
			}
		}

		Ticks.LastRun = Ticks.ThisRun;
	}

	public FuncSystem<TArg> RunIf(Func<bool> condition)
	{
		_conditions.Add((_, _) => condition());
		return this;
	}

	public FuncSystem<TArg> RunAfter(FuncSystem<TArg> parent)
	{
		if (this == parent || Contains(parent, s => s._after))
			throw new InvalidOperationException("Circular dependency detected");

		Node?.List?.Remove(Node);
		Node = parent._after.AddLast(this);

		return this;
	}

	public FuncSystem<TArg> RunAfter(params ReadOnlySpan<FuncSystem<TArg>> systems)
	{
		foreach (var system in systems) {
			system.RunAfter(this);
		}

		return this;
	}

	public FuncSystem<TArg> RunBefore(FuncSystem<TArg> parent)
	{
		if (this == parent || Contains(parent, s => s._before))
			throw new InvalidOperationException("Circular dependency detected");

		Node?.List?.Remove(Node);
		Node = parent._before.AddLast(this);

		return this;
	}

	public FuncSystem<TArg> RunBefore(params ReadOnlySpan<FuncSystem<TArg>> systems)
	{
		foreach (var system in systems) {
			system.RunBefore(this);
		}

		return this;
	}

	private bool Contains(FuncSystem<TArg> system, Func<FuncSystem<TArg>, LinkedList<FuncSystem<TArg>>> direction)
	{
		var current = this;
		while (current != null) {
			if (current == system)
				return true;

			var nextNode = direction(current)?.First;
			current = nextNode?.Value;
		}
		return false;
	}

	internal bool IsResourceInUse()
	{
		return _threadingType switch {
			ThreadingMode.Multi     => false,
			ThreadingMode.Single    => true,
			_ or ThreadingMode.Auto => _checkInUse()
		};
	}

	private bool ValidateConditions(SystemTicks ticks, TArg args)
	{
		foreach (var fn in _conditions) {
			if (!fn(ticks, args))
				return false;
		}
		return true;
	}
}

public enum Stages
{
	Startup,
	FrameStart,
	BeforeUpdate,
	Update,
	AfterUpdate,
	FrameEnd,

	OnEnter,
	OnExit
}

public enum ThreadingMode
{
	Auto,
	Single,
	Multi
}



public partial class Scheduler
{
	private readonly Dictionary<Type, IEventParam> _events = new Dictionary<Type, IEventParam>();
	private readonly List<FuncSystem<World>> _multiThreads = new List<FuncSystem<World>>();
	private readonly List<FuncSystem<World>> _singleThreads = new List<FuncSystem<World>>();
	private readonly LinkedList<FuncSystem<World>>[] _systems = new LinkedList<FuncSystem<World>>[(int)Stages.OnExit + 1];

	public Scheduler(World world, ThreadingMode threadingMode = ThreadingMode.Auto)
	{
		World = world;
		ThreadingExecutionMode = threadingMode;

		for (var i = 0; i < _systems.Length; ++i) {
			_systems[i] = new LinkedList<FuncSystem<World>>();
		}

		AddSystemParam(world);
		AddSystemParam(new SchedulerState(this));
		AddSystemParam(new Commands(world));
	}

	public World World { get; }
	public ThreadingMode ThreadingExecutionMode { get; }


	public void Run(Func<bool> checkForExitFn, Action? cleanupFn = null)
	{
		while (!checkForExitFn())
			RunOnce();

		cleanupFn?.Invoke();
	}

	public void RunOnce()
	{
		var ticks = World.Update();

		foreach (var (_, ev) in _events) {
			ev.Clear();
		}

		RunStage(Stages.Startup, ticks);
		_systems[(int)Stages.Startup].Clear();

		RunStage(Stages.OnExit, ticks);
		RunStage(Stages.OnEnter, ticks);

		for (var stage = Stages.FrameStart; stage <= Stages.FrameEnd; stage += 1) {
			RunStage(stage, ticks);
		}
	}

	private void RunStage(Stages stage, uint ticks)
	{
		_singleThreads.Clear();
		_multiThreads.Clear();

		var systems = _systems[(int)stage];

		if (systems.Count == 0)
			return;

		foreach (var sys in systems) {
			if (sys.IsResourceInUse()) {
				_singleThreads.Add(sys);
			} else {
				_multiThreads.Add(sys);
			}
		}

		var multithreading = _multiThreads;
		var singlethreading = _singleThreads;

		if (multithreading.Count > 0)
			Parallel.ForEach(multithreading, s => s.Run(ticks));

		foreach (var system in singlethreading) {
			system.Run(ticks);
		}
	}

	internal void Add(FuncSystem<World> sys, Stages stage)
	{
		sys.Node = _systems[(int)stage].AddLast(sys);
	}

	public FuncSystem<World> AddSystem(Action system, Stages stage = Stages.Update, ThreadingMode? threadingType = null)
	{
		if (!threadingType.HasValue)
			threadingType = ThreadingExecutionMode;

		var sys = new FuncSystem<World>(World, (ticks, args, runIf) => {
			if (runIf?.Invoke(ticks, args) ?? true) {
				system();
				return true;
			}
			return false;
		}, () => false, stage, threadingType.Value);
		Add(sys, stage);

		return sys;
	}

	public FuncSystem<World> OnEnter<TState>(TState st, Action system, ThreadingMode? threadingType = null)
		where TState : struct, Enum
	{
		if (!threadingType.HasValue)
			threadingType = ThreadingExecutionMode;

		var stateChangeId = -1;

		var sys = new FuncSystem<World>(World, (ticks, args, runIf) => {
				if (runIf?.Invoke(ticks, args) ?? true) {
					system();
					return true;
				}
				return false;
			}, () => false, Stages.OnEnter, threadingType.Value)
			.RunIf((State<TState> state) => state.ShouldEnter(st, ref stateChangeId));

		Add(sys, Stages.OnEnter);

		return sys;
	}

	public FuncSystem<World> OnExit<TState>(TState st, Action system, ThreadingMode? threadingType = null)
		where TState : struct, Enum
	{
		if (!threadingType.HasValue)
			threadingType = ThreadingExecutionMode;

		var stateChangeId = -1;

		var sys = new FuncSystem<World>(World, (ticks, args, runIf) => {
				if (runIf?.Invoke(ticks, args) ?? true) {
					system();
					return true;
				}
				return false;
			}, () => false, Stages.OnExit, threadingType.Value)
			.RunIf((State<TState> state) => state.ShouldExit(st, ref stateChangeId));

		Add(sys, Stages.OnExit);

		return sys;
	}

	public Scheduler AddPlugin<T>() where T : notnull, IPlugin, new()
		=> AddPlugin(new T());

	public Scheduler AddPlugin<T>(T plugin) where T : IPlugin
	{
		plugin.Build(this);

		return this;
	}

	public Scheduler AddEvent<T>() where T : notnull
	{
		if (_events.ContainsKey(typeof(T)))
			return this;

		var ev = new EventParam<T>();
		_events.Add(typeof(T), ev);
		return AddSystemParam(ev);
	}

	public Scheduler AddState<T>(T initialState = default!) where T : struct, Enum
	{
		var state = new State<T>(initialState, initialState);
		return AddSystemParam(state);
	}

	public Scheduler AddResource<T>(T resource) where T : notnull => AddSystemParam(new Res<T> { Value = resource });

	public Scheduler AddSystemParam<T>(T param) where T : notnull, ISystemParam<World>
	{
		World.Entity<Placeholder<T>>().Set(new Placeholder<T> { Value = param });

		return this;
	}

	internal bool ResourceExists<T>() where T : notnull, ISystemParam<World> => World.Entity<Placeholder<T>>().Has<Placeholder<T>>();

	internal bool InState<T>(T state) where T : struct, Enum
	{
		if (!World.Entity<Placeholder<State<T>>>().Has<Placeholder<State<T>>>())
			return false;
		return World.Entity<Placeholder<State<T>>>().Get<Placeholder<State<T>>>().Value.InState(state);
	}
}
**/
internal struct Placeholder<T> where T : ISystemParam
{
	public T Value;
}

public interface IPlugin
{
	void Build();
}

public interface IEventParam
{
	void Clear();
}

internal sealed class EventParam<T> : IEventParam, IIntoSystemParam<EventParam<T>>, ISystemParam where T : notnull
{
	private readonly List<T> _eventsLastFrame = new List<T>(), _eventsThisFrame = new List<T>();

	internal EventParam()
	{
		Writer = new EventWriter<T>(_eventsThisFrame);
		Reader = new EventReader<T>(_eventsLastFrame);
	}

	public EventWriter<T> Writer { get; }
	public EventReader<T> Reader { get; }


	public void Clear()
	{
		_eventsLastFrame.Clear();
		_eventsLastFrame.AddRange(_eventsThisFrame);
		_eventsThisFrame.Clear();
	}

	public static EventParam<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<EventParam<T>>>().Has<Placeholder<EventParam<T>>>())
			return arg.Entity<Placeholder<EventParam<T>>>().Get<Placeholder<EventParam<T>>>().Value;

		var ev = new EventParam<T>();
		arg.Entity<Placeholder<EventParam<T>>>().Set(new Placeholder<EventParam<T>> { Value = ev });
		return ev;
	}
	public void Init(ISystem system, World world) { }
	public bool Ready(ISystem system, World world) => true;
}

public sealed class EventWriter<T> : ISystemParam, IIntoSystemParam<EventWriter<T>> where T : notnull
{
	private readonly List<T> _events;

	internal EventWriter(List<T> events)
	{
		_events = events;
	}

	public bool IsEmpty
		=> _events.Count == 0;

	public static EventWriter<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<EventParam<T>>>().Has<Placeholder<EventParam<T>>>())
			return arg.Entity<Placeholder<EventParam<T>>>().Get<Placeholder<EventParam<T>>>().Value.Writer;

		throw new NotImplementedException("EventWriter<T> must be created using the scheduler.AddEvent<T>() method");
	}

	public void Clear()
		=> _events.Clear();

	public void Enqueue(T ev)
		=> _events.Add(ev);

	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredWrite(world.GetComponent<Placeholder<EventParam<T>>>().Id);
	}
	public bool Ready(ISystem system, World world) => true;
}

public sealed class EventReader<T> : ISystemParam, IIntoSystemParam<EventReader<T>> where T : notnull
{
	private readonly List<T> _events;

	internal EventReader(List<T> queue)
	{
		_events = queue;
	}

	public bool IsEmpty
		=> _events.Count == 0;

	public IEnumerable<T> Values => _events;

	public static EventReader<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<EventParam<T>>>().Has<Placeholder<EventParam<T>>>())
			return arg.Entity<Placeholder<EventParam<T>>>().Get<Placeholder<EventParam<T>>>().Value.Reader;

		throw new NotImplementedException("EventReader<T> must be created using the scheduler.AddEvent<T>() method");
	}

	public void Clear()
		=> _events.Clear();

	public List<T>.Enumerator GetEnumerator()
		=> _events.GetEnumerator();
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.AddUnfilteredRead(world.GetComponent<Placeholder<EventParam<T>>>().Id);
	}
	public bool Ready(ISystem system, World world) => throw new NotImplementedException();
}

partial class World : ISystemParam, IIntoSystemParam<World>
{
	public static World Generate(World arg) => arg;
	public void Init(ISystem system, World world)
	{
		system.Meta.Access.WriteAll();
	}
	public bool Ready(ISystem system, World world) => true;
}

public class Query<TQueryData> : Query<TQueryData, Empty>, IIntoSystemParam<Query<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Query(Query query) : base(query) { }

	public new static Query<TQueryData> Generate(World arg)
	{
		if (arg.Entity<Placeholder<Query<TQueryData>>>().Has<Placeholder<Query<TQueryData>>>())
			return arg.Entity<Placeholder<Query<TQueryData>>>().Get<Placeholder<Query<TQueryData>>>().Value;

		var builder = arg.QueryBuilder();
		TQueryData.Build(builder);
		var q = new Query<TQueryData>(builder.Build());
		arg.Entity<Placeholder<Query<TQueryData>>>().Set(new Placeholder<Query<TQueryData>> { Value = q });
		return q;
	}
}

public class Query<TQueryData, TQueryFilter> : ISystemParam, IIntoSystemParam<Query<TQueryData, TQueryFilter>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
	where TQueryFilter : struct, IFilter<TQueryFilter>, allows ref struct
{
	private readonly Query _query;

	internal Query(Query query)
	{
		_query = query;
	}

	public static Query<TQueryData, TQueryFilter> Generate(World arg)
	{
		if (arg.Entity<Placeholder<Query<TQueryData, TQueryFilter>>>().Has<Placeholder<Query<TQueryData, TQueryFilter>>>())
			return arg.Entity<Placeholder<Query<TQueryData, TQueryFilter>>>().Get<Placeholder<Query<TQueryData, TQueryFilter>>>().Value;

		var builder = arg.QueryBuilder();
		TQueryData.Build(builder);
		TQueryFilter.Build(builder);
		var q = new Query<TQueryData, TQueryFilter>(builder.Build());
		arg.Entity<Placeholder<Query<TQueryData, TQueryFilter>>>().Set(new Placeholder<Query<TQueryData, TQueryFilter>> { Value = q });
		return q;
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
	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) => new QueryIter<TQueryData, TQueryFilter>(_system.Meta.Ticks.LastRun, _system.Meta.Ticks.ThisRun, id == 0 ? _query.Iter() : _query.Iter(id));
	public void Init(ISystem system, World world)
	{
		_system = system;
		// TODO
	}
	public bool Ready(ISystem system, World world) => true;
}

public class Single<TQueryData> : Single<TQueryData, Empty>, IIntoSystemParam<Single<TQueryData>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
{
	internal Single(Query query) : base(query) { }

	public new static Single<TQueryData> Generate(World arg)
	{
		if (arg.Entity<Placeholder<Single<TQueryData>>>().Has<Placeholder<Single<TQueryData>>>())
			return arg.Entity<Placeholder<Single<TQueryData>>>().Get<Placeholder<Single<TQueryData>>>().Value;

		var builder = arg.QueryBuilder();
		TQueryData.Build(builder);
		var q = new Single<TQueryData>(builder.Build());
		arg.Entity<Placeholder<Single<TQueryData>>>().Set(new Placeholder<Single<TQueryData>> { Value = q });
		return q;
	}
}

public class Single<TQueryData, TQueryFilter> : ISystemParam, IIntoSystemParam<Single<TQueryData, TQueryFilter>>
	where TQueryData : struct, IData<TQueryData>, IQueryIterator<TQueryData>, allows ref struct
	where TQueryFilter : struct, IFilter<TQueryFilter>, allows ref struct
{
	private readonly Query _query;

	internal Single(Query query)
	{
		_query = query;
	}

	public static Single<TQueryData, TQueryFilter> Generate(World arg)
	{
		if (arg.Entity<Placeholder<Single<TQueryData, TQueryFilter>>>().Has<Placeholder<Single<TQueryData, TQueryFilter>>>())
			return arg.Entity<Placeholder<Single<TQueryData, TQueryFilter>>>().Get<Placeholder<Single<TQueryData, TQueryFilter>>>().Value;

		var builder = arg.QueryBuilder();
		TQueryData.Build(builder);
		TQueryFilter.Build(builder);
		var q = new Single<TQueryData, TQueryFilter>(builder.Build());
		arg.Entity<Placeholder<Single<TQueryData, TQueryFilter>>>().Set(new Placeholder<Single<TQueryData, TQueryFilter>> { Value = q });
		return q;
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
	private QueryIter<TQueryData, TQueryFilter> GetIter(EcsID id = 0) => new QueryIter<TQueryData, TQueryFilter>(_system.Meta.Ticks.LastRun, _system.Meta.Ticks.ThisRun, id == 0 ? _query.Iter() : _query.Iter(id));
	public void Init(ISystem system, World world)
	{
		this._system = system;
		// TODO
	}
	public bool Ready(ISystem system, World world) => true;
}

public sealed class State<T>(T previous, T current) : ISystemParam, IIntoSystemParam<State<T>> where T : struct, Enum
{
	private int _stateChangeId = -1;

	internal T Previous { get; private set; } = previous;
	public T Current { get; private set; } = current;

	public static State<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<State<T>>>().Has<Placeholder<State<T>>>())
			return arg.Entity<Placeholder<State<T>>>().Get<Placeholder<State<T>>>().Value;

		var state = new State<T>(default, default);
		arg.Entity<Placeholder<State<T>>>().Set(new Placeholder<State<T>> { Value = state });
		return state;
	}

	public void Set(T value)
	{
		if (!Equals(Current, value)) {
			Previous = Current;
			Current = value;
			_stateChangeId++; // Increment the change counter
		}
	}

	internal bool InState(T? state) => Equals(Current, state);

	internal int GetChangeId() => _stateChangeId;

	internal bool ShouldEnter(T state, ref int lastProcessedChangeId)
	{
		if (!Equals(Current, state))
			return false;

		if (lastProcessedChangeId != _stateChangeId) {
			lastProcessedChangeId = _stateChangeId;
			return true;
		}
		return false;
	}

	internal bool ShouldExit(T state, ref int lastProcessedChangeId)
	{
		if (!Equals(Previous, state))
			return false;

		if (lastProcessedChangeId != _stateChangeId) {
			lastProcessedChangeId = _stateChangeId;
			return true;
		}
		return false;
	}
	public void Init(ISystem system, World world)
	{
		throw new NotImplementedException();
	}
	public bool Ready(ISystem system, World world) => throw new NotImplementedException();
}

public class Res<T> : ISystemParam, IIntoSystemParam<Res<T>> where T : notnull
{
	private T? _t;

	public ref T? Value => ref _t;

	public static Res<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<Res<T>>>().Has<Placeholder<Res<T>>>())
			return arg.Entity<Placeholder<Res<T>>>().Get<Placeholder<Res<T>>>().Value;

		var res = new Res<T>();
		arg.Entity<Placeholder<Res<T>>>().Set(new Placeholder<Res<T>> { Value = res });
		return res;
	}

	public static implicit operator T?(Res<T> reference)
		=> reference.Value;
	public void Init(ISystem system, World world) { }
	public bool Ready(ISystem system, World world) => true;
}

public class ResRO<T> : Res<T> where T : notnull
{
	public void Init(ISystem system, World world) { }
}

public sealed class Local<T> : ISystemParam, IIntoSystemParam<Local<T>>
	where T : notnull
{
	private T? _t;

	public ref T? Value => ref _t;

	public static Local<T> Generate(World arg) => new Local<T>();

	public static implicit operator T?(Local<T> reference)
		=> reference.Value;
	public void Init(ISystem system, World world) { }
	public bool Ready(ISystem system, World world) => true;
}

/**
public sealed class SchedulerState : ISystemParam, IIntoSystemParam<SchedulerState>
{
	private readonly Scheduler _scheduler;

	internal SchedulerState(Scheduler scheduler)
	{
		_scheduler = scheduler;
	}

	public static ISystemParam Generate(World arg)
	{
		if (arg.Entity<Placeholder<SchedulerState>>().Has<Placeholder<SchedulerState>>())
			return arg.Entity<Placeholder<SchedulerState>>().Get<Placeholder<SchedulerState>>().Value;
		throw new NotImplementedException();
	}

	public void AddResource<T>(T resource) where T : notnull
		=> _scheduler.AddResource(resource);

	public bool ResourceExists<T>() where T : notnull
		=> _scheduler.ResourceExists<Res<T>>();

	public ref T? GetResource<T>() where T : notnull
	{
		if (_scheduler.ResourceExists<Res<T>>())
			return ref _scheduler.World.Entity<Placeholder<Res<T>>>().Get<Placeholder<Res<T>>>().Value.Value;
		throw new InvalidOperationException($"Resource of type {typeof(T)} does not exist.");
	}

	public void AddState<T>(T state = default!) where T : struct, Enum
		=> _scheduler.AddState(state);

	public bool InState<T>(T state) where T : struct, Enum
		=> _scheduler.InState(state);
}
**/

public sealed class Commands : ISystemParam, IIntoSystemParam<Commands>
{
	private readonly World _world;

	internal Commands(World world)
	{
		_world = world;
	}

	public static Commands Generate(World arg)
	{
		if (arg.Entity<Placeholder<Commands>>().Has<Placeholder<Commands>>())
			return arg.Entity<Placeholder<Commands>>().Get<Placeholder<Commands>>().Value;
		throw new NotImplementedException();
	}

	public EntityCommand Entity(EcsID id = 0)
	{
		var ent = _world.Entity(id);
		return new EntityCommand(_world, ent.ID);
	}
	public void Init(ISystem system, World world) { }
	public bool Ready(ISystem system, World world) => true;
}

public readonly ref struct EntityCommand
{
	private readonly World _world;

	internal EntityCommand(World world, EcsID id)
	{
		(_world, ID) = (world, id);
	}


	public readonly EcsID ID;

	public readonly EntityCommand Set<T>(T component) where T : struct
	{
		_world.SetDeferred(ID, component);
		return this;
	}

	public readonly EntityCommand Add<T>() where T : struct
	{
		_world.AddDeferred<T>(ID);
		return this;
	}

	public readonly EntityCommand Unset<T>() where T : struct
	{
		_world.UnsetDeferred<T>(ID);
		return this;
	}

	public readonly EntityCommand Delete()
	{
		_world.DeleteDeferred(ID);
		return this;
	}
}

public interface ITermCreator
{
	public static abstract void Build(QueryBuilder builder);
}

public interface IQueryIterator<TData>
	where TData : struct, allows ref struct
{

	[UnscopedRef]
	ref TData Current { get; }
	TData GetEnumerator();

	bool MoveNext();
}

public interface IData<TData> : ITermCreator, IQueryIterator<TData>
	where TData : struct, allows ref struct
{
	public static abstract TData CreateIterator(QueryIterator iterator);
}

public interface IFilter<TFilter> : ITermCreator, IQueryIterator<TFilter>
	where TFilter : struct, allows ref struct
{
	void SetTicks(uint lastRun, uint thisRun);
	public static abstract TFilter CreateIterator(QueryIterator iterator);
}

public ref struct Empty : IData<Empty>, IFilter<Empty>
{
	private readonly bool _asFilter;
	private QueryIterator _iterator;

	internal Empty(QueryIterator iterator, bool asFilter)
	{
		_iterator = iterator;
		_asFilter = asFilter;
	}

	public static void Build(QueryBuilder builder) { }


	[UnscopedRef]
	public ref Empty Current => ref this;

	public readonly void Deconstruct(out ReadOnlySpan<EntityView> entities, out int count)
	{
		entities = _iterator.Entities();
		count = entities.Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Empty GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool MoveNext() => _asFilter || _iterator.Next();

	public readonly void SetTicks(uint lastRun, uint thisRun) { }

	static Empty IData<Empty>.CreateIterator(QueryIterator iterator) => new Empty(iterator, false);

	static Empty IFilter<Empty>.CreateIterator(QueryIterator iterator) => new Empty(iterator, true);
}

