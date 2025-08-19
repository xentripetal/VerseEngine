namespace Verse.ECS;

public sealed partial class World : IDisposable
{
	public ComponentRegistry Registry;

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
	internal EcsID LastArchetypeId { get; set; }
	internal RelationshipEntityMapper RelationshipEntityMapper { get; }
	internal NamingEntityMapper NamingEntityMapper { get; }

	public uint Update() => ++_ticks;
	
	public ulong CurTick => _ticks;

	internal ref EcsRecord NewId(out EcsID newId, ulong id = 0)
	{
		ref var record = ref id > 0
			? ref _entities.Add(id, default!)
			: ref _entities.CreateNew(out id);

		newId = id;
		return ref record;
	}

	internal ref readonly SlimComponent GetComponent<T>() where T : struct
	{
		// todo - is ref here actually faster?
		ref readonly var lookup = ref Registry.GetSlimComponent<T>();
		ref var idx = ref _cachedComponents.GetOrCreate(lookup.Id, out var exists);
		if (!exists) {
			idx = Entity(lookup.Id).Set(lookup).ID;
			NamingEntityMapper.SetName(idx, Component<T>.StaticName);
		}

		return ref lookup;
	}


	internal ref EcsRecord GetRecord(EcsID id)
	{
		ref var record = ref _entities.Get(id);
		if (Unsafe.IsNullRef(ref record))
			EcsAssert.Panic(false, $"entity {id} is dead or doesn't exist!");
		return ref record;
	}

	private void Detach(EcsID entity, ulong id)
	{
		ref var record = ref GetRecord(entity);
		var oldArch = record.Archetype;

		if (oldArch.GetAnyIndex(id) < 0)
			return;

		OnComponentUnset?.Invoke(this, entity, new SlimComponent(id, -1));

		BeginDeferred();

		var foundArch = oldArch.TraverseLeft(id);
		if (foundArch == null && oldArch.All.Length - 1 <= 0) {
			foundArch = Root;
		}

		if (foundArch == null) {
			var hash = 0ul;
			foreach (ref readonly var cmp in oldArch.All.AsSpan()) {
				if (cmp.Id != id)
					hash = UnorderedSetHasher.Combine(hash, cmp.Id);
			}

			if (!Archetypes.TryGetFromHashId(hash, out foundArch)) {
				var arr = new SlimComponent[oldArch.All.Length - 1];
				for (int i = 0, j = 0; i < oldArch.All.Length; ++i) {
					ref readonly var item = ref oldArch.All[i];
					if (item.Id != id)
						arr[j++] = item;
				}

				foundArch = NewArchetype(oldArch, arr, id);
			}
		}

		record.Chunk = record.Archetype.MoveEntity(foundArch!, ref record.Chunk, record.Row, true, out record.Row);
		record.Archetype = foundArch!;
		EndDeferred();

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

		BeginDeferred();

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
		EndDeferred();

		OnComponentSet?.Invoke(this, entity, component);

		column = component.IsTag ? foundArch.GetAnyIndex(component.Id) : foundArch.GetComponentIndex(component.Id);
		if (!component.IsTag) {
			record.Chunk.MarkAdded(column, record.Row, _ticks);
		}
		return (component.IsTag ? null : record.Chunk.Columns![column].Data, record.Row);
	}

	internal bool IsAttached(ref EcsRecord record, EcsID id)
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

	internal ref T GetUntrusted<T>(EcsID entity, EcsID id, int size) where T : struct
	{
		if (IsDeferred && !Has(entity, id)) {
			Unsafe.SkipInit<T>(out var val);
			return ref Unsafe.Unbox<T>(SetDeferred(entity, id, val, size)!);
		}

		ref var record = ref GetRecord(entity);
		var column = record.Archetype.GetComponentIndex(id);
		return ref record.Chunk.GetReferenceAt<T>(column, record.Row);
	}
	internal delegate Query QueryFactoryDel(World world, ReadOnlySpan<IQueryTerm> terms);
}

internal struct EcsRecord
{
	public Archetype Archetype;
	public int Row;
	public ArchetypeChunk Chunk;
}