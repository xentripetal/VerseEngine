namespace Verse.ECS;

public sealed partial class World : IDisposable
{
	public ComponentRegistry Registry;
	public EventRegistry EventRegistry;

	private static readonly Comparison<SlimComponent> _comparisonCmps = (a, b)
		=> ComponentComparer.CompareTerms(a.Id, b.Id);
	private static readonly Comparison<IQueryTerm> _comparisonTerms = (a, b)
		=> a.CompareTo(b);

	private readonly FastIdLookup<EcsID> _cachedComponents = new FastIdLookup<EcsID>();
	private readonly ComponentComparer _comparer;
	private readonly EntitySparseSet<EcsRecord> _entities = new EntitySparseSet<EcsRecord>();
	private readonly object _newEntLock = new object();
	private uint _ticks;
	private ulong _archetypeGeneration;

	internal Archetype Root { get; }
	internal Resources Resources { get; }
	internal EcsID LastArchetypeId { get; set; }
	internal RelationshipEntityMapper RelationshipEntityMapper { get; }
	internal NamingEntityMapper NamingEntityMapper { get; }

	/// <summary>
	/// Reads the current change tick of this world.
	/// </summary>
	public Tick ChangeTick()
	{
		return new Tick(_ticks);
	}

	public uint Update()
	{
		++_ticks;
		// TODO track component removals and clear here
		EventRegistry.Update(this, _ticks);

		return _ticks;
	}

	public void Init()
	{
		if (_ticks == 0)
			_ticks = 1;
	}

	public uint CurTick => _ticks;

	internal ref EcsRecord NewId(out EcsID newId, ulong id = 0)
	{
		ref var record = ref id > 0
			? ref _entities.Add(id, default!)
			: ref _entities.CreateNew(out id);

		newId = id;
		return ref record;
	}

	public ref readonly SlimComponent GetComponent<T>() 
	{
		// todo - is ref here actually faster?
		return ref Registry.GetSlimComponent<T>();
	}


	internal ref EcsRecord GetRecord(EcsID id)
	{
		ref var record = ref _entities.Get(id);
		if (Unsafe.IsNullRef(ref record))
			EcsAssert.Panic(false, $"entity {id} is dead or doesn't exist!");
		return ref record;
	}

	private void Detach(EcsID entity, ComponentId component)
	{
		ref var record = ref GetRecord(entity);
		var oldArch = record.Archetype;

		if (oldArch.GetAnyIndex(component) < 0)
			return;

		OnComponentUnset?.Invoke(this, entity, new SlimComponent(component, -1));

		// TODO: do we need to lock

		var foundArch = oldArch.TraverseLeft(component);
		if (foundArch == null && oldArch.All.Length - 1 <= 0) {
			foundArch = Root;
		}

		if (foundArch == null) {
			var hash = 0ul;
			foreach (ref readonly var cmp in oldArch.All.AsSpan()) {
				if (cmp.Id != component)
					hash = UnorderedSetHasher.Combine(hash, cmp.Id);
			}

			if (!Archetypes.TryGetFromHashId(hash, out foundArch)) {
				var arr = new SlimComponent[oldArch.All.Length - 1];
				for (int i = 0, j = 0; i < oldArch.All.Length; ++i) {
					ref readonly var item = ref oldArch.All[i];
					if (item.Id != component)
						arr[j++] = item;
				}

				foundArch = NewArchetype(oldArch, arr, component);
			}
		}

		record.Chunk = record.Archetype.MoveEntity(foundArch!, ref record.Chunk, record.Row, true, out record.Row);
		record.Archetype = foundArch!;
		// TODO : end lock

	}

	private (Array?, int) Attach(EcsID entity, ref readonly SlimComponent component)
	{
		ref var record = ref GetRecord(entity);
		var oldArch = record.Archetype;

		var column = component.IsTag ? oldArch.GetAnyIndex(component.Id) : oldArch.GetComponentIndex(component.Id);
		if (column >= 0) {
			if (!component.IsTag) {
				record.Chunk.MarkChanged(column, record.Row, _ticks);
			}
			return (component.IsTag ? null : record.Chunk.Columns![column].Data, record.Row);
		}


		// TODO : Do we need to lock?
		var foundArch = oldArch.TraverseRight(component.Id);
		if (foundArch == null) {
			var hash = 0ul;

			var found = false;
			foreach (ref readonly var cmp in oldArch.All.AsSpan()) {
				if (!found && cmp.Id > component.Id) {
					hash = UnorderedSetHasher.Combine(hash, component.Id);
					found = true;
				}

				hash = UnorderedSetHasher.Combine(hash, cmp.Id);
			}

			if (!found)
				hash = UnorderedSetHasher.Combine(hash, component.Id);

			if (!Archetypes.TryGetFromHashId(hash, out foundArch)) {
				var arr = new SlimComponent[oldArch.All.Length + 1];
				oldArch.All.CopyTo(arr, 0);
				arr[^1] = component;
				arr.AsSpan().Sort(_comparisonCmps);

				foundArch = NewArchetype(oldArch, arr, component.Id);
			}
		}

		record.Chunk = record.Archetype.MoveEntity(foundArch!, ref record.Chunk, record.Row, false, out record.Row);
		record.Archetype = foundArch!;
		// TODO : end lock

		OnComponentSet?.Invoke(this, entity, component);

		column = component.IsTag ? foundArch.GetAnyIndex(component.Id) : foundArch.GetComponentIndex(component.Id);
		if (!component.IsTag) {
			record.Chunk.MarkAdded(column, record.Row, _ticks);
			record.Chunk.MarkChanged(column, record.Row, _ticks);
		}
		return (component.IsTag ? null : record.Chunk.Columns![column].Data, record.Row);
	}

	internal bool IsAttached(ref EcsRecord record, ComponentId id)
	{
		return record.Archetype.HasIndex(id);
	}

	private Archetype NewArchetype(Archetype oldArch, SlimComponent[] sign, EcsID hashId)
	{
		var archetype = Root.InsertVertex(oldArch, sign, hashId, _archetypeGeneration++);
		Archetypes.Add(archetype);
		LastArchetypeId = archetype.HashId;
		return archetype;
	}

	internal ref T GetUntrusted<T>(EcsID entity, EcsID id, int size) 
	{
		ref var record = ref GetRecord(entity);
		var column = record.Archetype.GetComponentIndex(id);
		return ref record.Chunk.GetReferenceAt<T>(column, record.Row);
	}
}

internal struct EcsRecord
{
	public Archetype Archetype;
	public int Row;
	public ArchetypeChunk Chunk;
}