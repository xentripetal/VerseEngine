using Serilog;
using Verse.ECS.Scheduling;
using Verse.ECS.Systems;

namespace Verse.ECS;

/// <summary>
///     The entities container.
/// </summary>
public sealed partial class World
{
	/// <summary>
	///     Create the world.
	/// </summary>
	public World()
	{
		Registry = new ComponentRegistry(this);
		EventRegistry = new EventRegistry();
		Archetypes = new ArchetypeRegistry();
		_comparer = new ComponentComparer();
		Resources = new Resources(Registry);
		Root = new Archetype(
			this,
			[],
			_comparer,
			_archetypeGeneration++
		);
		Archetypes.Add(Root);
		LastArchetypeId = Root.HashId;

		RelationshipEntityMapper = new RelationshipEntityMapper(this);
		NamingEntityMapper = new NamingEntityMapper(this);

		OnPluginInitialization?.Invoke(this);
	}


	public ArchetypeRegistry Archetypes { get; }


	/// <summary>
	///     Count of entities alive.<br />
	///     ⚠️ If the count doesn't match with your expectations it's because
	///     in TinyEcs components are also entities!
	/// </summary>
	public int EntityCount => _entities.Length;

	/// <summary>
	///     Cleanup the world.
	/// </summary>
	public void Dispose()
	{
		_entities.Clear();
		Root.Clear();
		Archetypes.Clear();
		RelationshipEntityMapper.Clear();
		NamingEntityMapper.Clear();
		EventRegistry.Clear();
	}


	public event Action<World, EcsID>? OnEntityCreated, OnEntityDeleted;
	public event Action<World, EcsID, SlimComponent>? OnComponentSet, OnComponentUnset;
	public static event Action<World>? OnPluginInitialization;

	/// <summary>
	///     Remove all empty archetypes.
	/// </summary>
	/// <returns></returns>
	public int RemoveEmptyArchetypes()
	{
		var removed = 0;
		Root?.RemoveEmptyArchetypes(ref removed, Archetypes);
		if (removed > 0)
			LastArchetypeId = ulong.MaxValue;
		return removed;
	}

	/// <summary>
	///     Get or create an archetype with the specified components.
	/// </summary>
	/// <param name="ids"></param>
	/// <returns></returns>
	public Archetype Archetype(params Span<SlimComponent> ids)
	{
		if (ids.IsEmpty)
			return Root;

		ids.Sort(_comparisonCmps);

		var hash = 0ul;
		foreach (ref readonly var cmp in ids) {
			hash = UnorderedSetHasher.Combine(hash, cmp.Id);
		}
		if (!Archetypes.TryGetFromHashId(hash, out var archetype)) {
			var archLessOne = Archetype(ids[..^1]);
			var arr = new SlimComponent[ids.Length];
			archLessOne.All.CopyTo(arr, 0);
			arr[^1] = ids[^1];
			arr.AsSpan().Sort(_comparisonCmps);
			archetype = NewArchetype(archLessOne, arr, arr[^1].Id);
		}

		return archetype;
	}

	/// <summary>
	///     Create an entity with the specified components attached.
	/// </summary>
	/// <param name="arch"></param>
	/// <returns></returns>
	public EntityView Entity(Archetype arch)
	{
		ref var record = ref NewId(out var id);
		record.Archetype = arch;
		record.Chunk = arch.Add(id, out record.Row);
		return new EntityView(this, id);
	}

	/// <summary>
	///     Create or get an entity with the specified <paramref name="id" />.<br />
	///     When <paramref name="id" /> is not specified or is 0 a new entity is spawned.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public EntityView Entity(ulong id = 0)
	{
		lock (_newEntLock) {
			EntityView ent;
			if (id == 0 || !Exists(id)) {
				ref var record = ref NewId(out id, id);
				record.Archetype = Root;
				record.Chunk = Root.Add(id, out record.Row);
				ent = new EntityView(this, id);
				OnEntityCreated?.Invoke(this, id);
			} else {
				ent = new EntityView(this, id);
			}
			return ent;
		}
	}




	/// <summary>
	///     Create or get an entity using the specified <paramref name="name" />.<br />
	///     A relation (Identity, Name) will be automatically added to the entity.
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public EntityView Entity(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return EntityView.Invalid;

		var entity = new EntityView(this, NamingEntityMapper.SetName(0, name));

		return entity;
	}

	public void RunSystem(ISystem system)
	{
		system.TryRun(this, CurTick);
	}

	public bool EvalCondition(ICondition condition)
	{
		return condition.Evaluate(this, CurTick);
	}

	/// <summary>
	///     Delete the entity.<br />
	///     Associated children are deleted too.
	/// </summary>
	/// <param name="entity"></param>
	public void Delete(EcsID entity)
	{
		if (!Exists(entity)) return;
		lock (_newEntLock) {
			OnEntityDeleted?.Invoke(this, entity);

			ref var record = ref GetRecord(entity);
			var removedId = record.Archetype.Remove(ref record);
			EcsAssert.Assert(removedId == entity);
			_entities.Remove(removedId);
		}
	}

	/// <summary>
	///     Check if the entity is valid and alive.
	/// </summary>
	/// <param name="entity"></param>
	/// <returns></returns>
	public bool Exists(EcsID entity) => _entities.Contains(entity);

	/// <summary>
	///     Mark the component as changed.<br />
	/// </summary>
	public void SetChanged(EcsID entity, EcsID component)
	{
		ref var record = ref GetRecord(entity);
		var index = record.Archetype.GetComponentIndex(component);
		EcsAssert.Panic(index >= 0, "Component not found in the entity");
		record.Chunk.MarkChanged(index, record.Row, _ticks);
	}

	/// <inheritdoc cref="SetChanged" />
	public void SetChanged<T>(EcsID entity) 
	{
		SetChanged(entity, GetComponent<T>().Id);
	}

	/// <summary>
	///     Use this function to analyze pairs members.<br />
	///     Pairs members lose their generation count. This function will bring it back!.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public EcsID GetAlive(EcsID id)
	{
		if (Exists(id))
			return id;

		if ((uint)id != id)
			return 0;

		var current = _entities.GetNoGeneration(id);
		if (current == 0)
			return 0;

		if (!Exists(current))
			return 0;

		return current;
	}

	/// <summary>
	///     The archetype sign.<br />The sign is unique.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public ReadOnlySpan<SlimComponent> GetSlimType(EcsID id)
	{
		ref var record = ref GetRecord(id);
		return record.Archetype.All.AsSpan();
	}

	/// <summary>
	///     The archetype sign.<br />The sign is unique.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public ReadOnlySpan<Component> GetType(EcsID id)
	{
		ref var record = ref GetRecord(id);
		var slim = record.Archetype.All.AsSpan();
		var components = new Component[slim.Length];
		for (int i = 0; i < slim.Length; i++) {
			components[i] = Registry.GetComponent(slim[i].Id);
		}
		return components;
	}

	/// <summary>
	///     Add a Tag to the entity.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="entity"></param>
	public void Add<T>(EcsID entity) 
	{
		ref readonly var cmp = ref GetComponent<T>();
		EcsAssert.Panic(cmp.Size <= 0, "this is not a tag");
		_ = Attach(entity, in cmp);
	}

	public void Add(EcsID entity, ComponentId id)
	{
		var c = new SlimComponent(id, 0);
		_ = Attach(entity, in c);
	}

	/// <summary>
	///     Set a Component to the entity.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="entity"></param>
	/// <param name="component"></param>
	public void Set<T>(EcsID entity, T component) 
	{
		ref readonly var cmp = ref GetComponent<T>();
		EcsAssert.Panic(cmp.Size > 0, "this is not a component");
		var (raw, row) = Attach(entity, in cmp);
		var array = (T[])raw!;
		array[row & ECS.Archetype.CHUNK_THRESHOLD] = component;
	}

	/// <summary>
	///     Remove a component or a tag from the entity.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="entity"></param>
	public void Unset<T>(EcsID entity) 
		=> Unset(entity, GetComponent<T>().Id);

	/// <summary>
	///     Remove a component Id or a tag Id from the entity.
	/// </summary>
	/// <param name="entity"></param>
	/// <param name="id"></param>
	public void Unset(EcsID entity, ComponentId id)
	{
		Detach(entity, id);
	}

	/// <summary>
	///     Check if the entity has a component or tag.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="entity"></param>
	/// <returns></returns>
	public bool Has<T>(EcsID entity) 
		=> Has(entity, GetComponent<T>().Id);

	/// <summary>
	///     Check if the entity has a component or tag.<br />
	///     Component or tag is an entity.
	/// </summary>
	/// <param name="entity"></param>
	/// <param name="id"></param>
	/// <returns></returns>
	public bool Has(EcsID entity, ComponentId id) => IsAttached(ref GetRecord(entity), id);

	/// <summary>
	///     Get a component from the entity.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="entity"></param>
	/// <returns></returns>
	public ref T Get<T>(EcsID entity) 
	{
		ref readonly var cmp = ref GetComponent<T>();
		return ref GetUntrusted<T>(entity, cmp.Id, cmp.Size);
	}

	/// <summary>
	///     Get the name associated to the entity.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public string Name(EcsID id)
	{
		var name = NamingEntityMapper.GetName(id);
		if (!string.IsNullOrEmpty(name))
			return name;
		return string.Empty;
	}

	public void UnsetName(EcsID id)
	{
		NamingEntityMapper.UnsetName(id);
	}

	/// <summary>
	///     Print the archetype graph.
	/// </summary>
	public void PrintGraph()
	{
		Root.Print(0);
	}

	/// <summary>
	/// </summary>
	/// <returns></returns>
	public QueryBuilder QueryBuilder() => new QueryBuilder(this);


	/// <summary>
	///     Uncached query iterator
	/// </summary>
	/// <param name="terms"></param>
	/// <returns></returns>
	public QueryIterator GetQueryIterator(Span<IQueryTerm> terms)
	{
		terms.Sort(_comparisonTerms);
		return new QueryIterator(Root, terms);
	}

	public readonly ref struct QueryIterator
	{
		private readonly ReadOnlySpan<IQueryTerm> _terms;
		private readonly Stack<Archetype> _archetypeStack;

		internal QueryIterator(Archetype root, ReadOnlySpan<IQueryTerm> terms)
		{
			_terms = terms;
			_archetypeStack = Renting<Stack<Archetype>>.Rent();
			_archetypeStack.Clear();
			_archetypeStack.Push(root);
		}

		public bool Next(out Archetype? archetype)
		{
			while (_archetypeStack.TryPop(out archetype)) {
				var result = archetype.MatchWith(_terms);

				if (result == ArchetypeSearchResult.Stop)
					break;

				foreach (var edge in archetype._add) {
					_archetypeStack.Push(edge.Archetype);
				}

				if (result == 0 && archetype.Count > 0) {
					return true;
				}
			}

			archetype = null;
			return false; // No more archetypes to iterate over
		}

		public void Dispose()
		{
			_archetypeStack.Clear();
			Renting<Stack<Archetype>>.Return(_archetypeStack);
		}
	}

	/// <summary>
	///     Runs the <see cref="Schedule" /> associated with the label a single time
	/// </summary>
	/// <param name="label"></param>
	public void RunSchedule(string label)
	{
		ScheduleScope(label, (world, schedule) => { schedule.Run(world); });
	}

	/// <summary>
	///     Temporarily removes the schedule associated with label from the <see cref="ScheduleContainer" />, passes it to the
	///     provided fn, and finally re-adds it
	///     to the container.
	/// </summary>
	/// <param name="label">Label of schedule to scope</param>
	/// <param name="fn">Function to invoke with the schedule</param>
	/// <typeparam name="T">Return type from the scoped function</typeparam>
	/// <returns>response from fn</returns>
	public void ScheduleScope(string label, Action<World, Schedule> fn)
	{
		var schedules = Resource<ScheduleContainer>();
		var schedule = schedules.Remove(label);
		if (schedule == null) {
			throw new ArgumentException($"Schedule for label {label} not found");
		}

		fn(this, schedule);
		var old = schedules.Insert(schedule);
		if (old != null) {
			Log.Warning(
				"Schedule {Label} was inserted during a call to PolyWorld.ScheduleScope, its value has been overwritten",
				label);
		}
	}

	public void AllowAmbiguousComponent<T>() 
	{
		var schedules = Resource<ScheduleContainer>();
		schedules.AllowAmbiguousComponent(Registry.RegisterComponent<T>());
	}

	public void AllowAmbiguousResource<T>()
	{
		var schedules = Resource<ScheduleContainer>();
		schedules.AllowAmbiguousComponent(Registry.RegisterResource<T>());
	}

	public void AddMessage<T>() where T : notnull
	{
		var id = RegisterResource<Messages<T>>();
		var data = Resources.InitializeResource(id);
		if (!data.IsPresent) {
			var messages = new Messages<T>();
			data.Insert(messages, ChangeTick());
			EventRegistry.Register(messages);
		}
	}
	
	public void WriteMessage<T>(in T value) where T : notnull
	{
		var messages = GetResource<Messages<T>>();
		EcsAssert.Panic(messages != null, $"Messages<{typeof(T).FullName}> resource not found in world. Did you forget to call World.AddMessage<{typeof(T).FullName}>()?");
		messages!.Writer.Enqueue(value);
	}


	public bool TryRegisterRequiredComponentsWith<TComponent, TRequired>(Func<TRequired> ctor)
	{
		var requiree = Registry.RegisterComponent<TComponent>();
		// todo check if we already have an archetype with this component and panic if present
		var required = Registry.RegisterComponent<TRequired>();
		throw new NotImplementedException();
	} 


	public void RegisterRequiredComponents<TComponent, TRequired>() where TRequired : new()
	{
		if (!TryRegisterRequiredComponentsWith<TComponent, TRequired>(() => new TRequired())) {
			throw new InvalidOperationException($"Could not register required components for {typeof(TComponent).FullName}");
		}
	}

	public RawComponentHooks RegisterComponentHook<T>()
	{
		return Registry.GetComponent<T>().Hooks;
	}
}