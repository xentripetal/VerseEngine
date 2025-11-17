using Verse.ECS.Systems;

namespace Verse.ECS;

public sealed class QueryBuilder
{
	private readonly Dictionary<EcsID, IQueryTerm> _components = new Dictionary<EcsID, IQueryTerm>();
	private Query? _query;

	internal QueryBuilder(World world)
	{
		World = world;
	}

	public World World { get; }

	public QueryBuilder With<T>(TermAccess access = TermAccess.Read) => With(World.GetComponent<T>().Id, access);


	public QueryBuilder With(ComponentId id, TermAccess access = TermAccess.Read)
		=> Term(new WithTerm(id, access));

	public QueryBuilder Without<T>() 
		=> Without(World.GetComponent<T>().Id);

	public QueryBuilder Without(ComponentId id)
		=> Term(new WithoutTerm(id));

	public QueryBuilder Optional<T>() 
	{
		ref readonly var cmp = ref World.GetComponent<T>();
		EcsAssert.Panic(cmp.Size > 0, "You can't access Tag as Component");
		return Optional(cmp.Id);
	}

	public QueryBuilder Optional(ComponentId id)
		=> Term(new OptionalTerm(id));

	public QueryBuilder Term(IQueryTerm term)
	{
		_components[term.Id] = term;
		return this;
	}

	public Query Build()
	{
		_query = null;
		_query ??= new Query(World, _components.Values.ToArray());
		return _query;
	}
}

public sealed class Query
{
	private readonly int[] _indices;
	private readonly List<Archetype> _matchedArchetypes;
	private readonly IQueryTerm[] _terms;
	private EcsID _lastArchetypeIdMatched;

	internal Query(World world, IQueryTerm[] terms)
	{
		World = world;
		_matchedArchetypes = new List<Archetype>();

		_terms = new IQueryTerm[terms.Length];
		terms.CopyTo(_terms, 0);
		Array.Sort(_terms);

		Terms = terms.Where(s => World.Registry.GetSlimComponent(s.Id).Size > 0).ToArray();

		_indices = new int[Terms.Length];
		_indices.AsSpan().Fill(-1);
	}

	internal World World { get; }
	internal IQueryTerm[] Terms { get; }



	private void Match()
	{
		if (_lastArchetypeIdMatched == World.LastArchetypeId)
			return;

		_lastArchetypeIdMatched = World.LastArchetypeId;
		_matchedArchetypes.Clear();
		World.Root.GetSuperSets(_terms.AsSpan(), _matchedArchetypes);
	}

	public int Count()
	{
		Match();

		return _matchedArchetypes.Sum(static s => s.Count);
	}

	public QueryIterator Iter(Tick lastRun, Tick thisRun)
	{
		Match();

		return Iter(CollectionsMarshal.AsSpan(_matchedArchetypes), 0, -1, lastRun, thisRun);
	}

	public QueryIterator Iter(EcsID entity, Tick lastRun, Tick thisRun)
	{
		Match();

		if (World.Exists(entity)) {
			ref var record = ref World.GetRecord(entity);
			foreach (var arch in _matchedArchetypes) {
				if (arch.HashId != record.Archetype.HashId) continue;
				var archetypes = new ReadOnlySpan<Archetype>(ref record.Archetype);
				return Iter(archetypes, record.Row, 1, lastRun, thisRun);
			}
		}

		return Iter([], 0, 0, lastRun, thisRun);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private QueryIterator Iter(ReadOnlySpan<Archetype> archetypes, int start, int count, Tick lastRun, Tick thisRun) => new QueryIterator(archetypes, Terms, _indices, start, count, lastRun, thisRun);

	public FilteredAccess BuildAccess()
	{
		var filter = new FilteredAccess();
		foreach (var term in Terms) {
			switch (term.Op) {
				case TermOp.With:
					if (term.Access == TermAccess.Read) {
						filter.AddRead(term.Id);
					} else if (term.Access == TermAccess.Write) {
						filter.AddWrite(term.Id);
					}
					break;
				case TermOp.Without:
					filter.AndWithout(term.Id);
					break;
				case TermOp.Optional:
					var optionalAccess = new Access();
					if (term.Access == TermAccess.Read) {
						optionalAccess.AddRead(term.Id);
					} else if (term.Access == TermAccess.Write) {
						optionalAccess.AddWrite(term.Id);
					}
					filter.ExtendAccess(optionalAccess);
					break;
			}
		}
		return filter;
	}
}

[SkipLocalsInit]
public ref struct QueryIterator
{
	private ReadOnlySpan<Archetype>.Enumerator archetypeIterator;
	private ReadOnlySpan<ArchetypeChunk>.Enumerator chunkIterator;
	private readonly ReadOnlySpan<IQueryTerm> terms;
	private readonly Span<int> indices;
	private readonly int start, startSafe, count;
	public readonly Tick LastRun, ThisRun;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal QueryIterator(ReadOnlySpan<Archetype> archetypes, ReadOnlySpan<IQueryTerm> terms, Span<int> indices, int start, int count, Tick lastRun, Tick thisRun)
	{
		archetypeIterator = archetypes.GetEnumerator();
		this.terms = terms;
		this.indices = indices;
		this.start = start;
		startSafe = start & Archetype.CHUNK_THRESHOLD;
		this.count = count;
		LastRun = lastRun;
		ThisRun = thisRun;
	}

	public readonly int Count {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => count > 0 ? Math.Min(count, chunkIterator.Current.Count) : chunkIterator.Current.Count;
	}

	public readonly Archetype Archetype => archetypeIterator.Current;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int GetColumnIndexOf<T>() where T : struct => indices.IndexOf(archetypeIterator.Current.GetComponentIndex<T>());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly DataRow<T> GetColumn<T>(int index) 
	{
#if NET9_0_OR_GREATER
		Unsafe.SkipInit(out DataRow<T> data);
#else
		var data = new DataRow<T>();
#endif
		if (index < 0 || index >= indices.Length) {
			data.Value.Value = ref Unsafe.NullRef<T>();
			data.Value.AddedTick = ref Unsafe.NullRef<Tick>();
			data.Value.ChangedTick = ref Unsafe.NullRef<Tick>();
			data.Size = 0;
			return data;
		}

		var i = indices[index];
		if (i < 0) {
			data.Value.Value = ref Unsafe.NullRef<T>();
			data.Value.AddedTick = ref Unsafe.NullRef<Tick>();
			data.Value.ChangedTick = ref Unsafe.NullRef<Tick>();
			data.Size = 0;
			return data;
		}

		ref readonly var chunk = ref chunkIterator.Current;
		ref var column = ref chunk.GetColumn(i);
		ref var reference = ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(column.Data));
		ref var addedTicks = ref MemoryMarshal.GetArrayDataReference(column.AddedTicks);
		ref var changedTicks = ref MemoryMarshal.GetArrayDataReference(column.ChangedTicks);
		
		data.Size = Unsafe.SizeOf<T>();
		data.Value.AddedTick = ref addedTicks;
		data.Value.ChangedTick = ref changedTicks;
		data.Value.Value = ref Unsafe.Add(ref reference, startSafe);
		data.Value.AddedTick = ref Unsafe.Add(ref addedTicks, startSafe);
		data.Value.ChangedTick = ref Unsafe.Add(ref changedTicks, startSafe);

		return data;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly Span<Tick> GetChangedTicks(int index)
	{
		if (index >= indices.Length) {
			return Span<Tick>.Empty;
		}

		var i = indices[index];
		if (i < 0) {
			return Span<Tick>.Empty;
		}

		ref readonly var chunk = ref chunkIterator.Current;
		ref var column = ref chunk.GetColumn(i);
		ref var stateRef = ref MemoryMarshal.GetArrayDataReference(column.ChangedTicks);

		var span = MemoryMarshal.CreateSpan(ref stateRef, column.ChangedTicks.Length);
		if (!span.IsEmpty)
			span = span.Slice(startSafe, Count);
		return span;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly Span<Tick> GetAddedTicks(int index)
	{
		if (index >= indices.Length) {
			return Span<Tick>.Empty;
		}

		var i = indices[index];
		if (i < 0) {
			return Span<Tick>.Empty;
		}

		ref readonly var chunk = ref chunkIterator.Current;
		ref var column = ref chunk.GetColumn(i);
		ref var stateRef = ref MemoryMarshal.GetArrayDataReference(column.AddedTicks);

		var span = MemoryMarshal.CreateSpan(ref stateRef, column.AddedTicks.Length);
		if (!span.IsEmpty)
			span = span.Slice(startSafe, Count);
		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkChanged(int index, int row)
	{
		if (index >= indices.Length)
			return;
		var i = indices[index];

		ref readonly var chunk = ref chunkIterator.Current;
		ref var column = ref chunk.GetColumn(i);
		column.MarkChanged(row, ThisRun);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Span<T> Data<T>(int index) where T : struct
	{
		var span = chunkIterator.Current.GetSpan<T>(indices[index]);

		if (!span.IsEmpty)
			span = span.Slice(startSafe, Count);

		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<ROEntityView> Entities()
	{
		var entities = chunkIterator.Current.GetEntities();

		if (!entities.IsEmpty)
			entities = entities.Slice(startSafe, Count);

		return entities;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlyMemory<ROEntityView> EntitiesAsMemory()
	{
		var entities = chunkIterator.Current.Entities.AsMemory(0, chunkIterator.Current.Count);

		if (!entities.IsEmpty)
			entities = entities.Slice(startSafe, Count);

		return entities;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Next()
	{
	REDO:
		while (chunkIterator.MoveNext()) {
			if (chunkIterator.Current.Count > 0)
				return true;
		}

	REDO_1:
		if (!archetypeIterator.MoveNext())
			return false;

		if (archetypeIterator.Current.Count <= 0)
			goto REDO_1;

		ref readonly var arch = ref archetypeIterator.Current;
		for (var i = 0; i < indices.Length; ++i) {
			indices[i] = arch.GetComponentIndex(terms[i].Id);
		}
		chunkIterator = arch.Chunks[(start >> Archetype.CHUNK_LOG2)..].GetEnumerator();

		goto REDO;
	}
}