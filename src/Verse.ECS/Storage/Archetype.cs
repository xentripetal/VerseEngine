using System.Collections.Frozen;

namespace Verse.ECS;

[SkipLocalsInit]
internal readonly struct Column
{
	public readonly Array Data;
	public readonly uint[] ChangedTicks, AddedTicks;
	private readonly World _world;

	internal Column(World world, ref readonly SlimComponent slimComponent, int chunkSize)
	{
		_world = world;
		Data = world.Registry.GetArray(slimComponent.Id, chunkSize)!;
		ChangedTicks = new uint[chunkSize];
		AddedTicks = new uint[chunkSize];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear(int index)
	{
		Array.Clear(Data, index, 1);
		ChangedTicks[index] = 0;
		AddedTicks[index] = 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkChanged(int index, Tick tick)
	{
		ChangedTicks[index] = tick.Value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkAdded(int index, uint ticks)
	{
		AddedTicks[index] = ticks;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(int srcIdx, ref readonly Column dest, int dstIdx)
	{
		Array.Copy(Data, srcIdx, dest.Data, dstIdx, 1);
		dest.ChangedTicks[dstIdx] = ChangedTicks[srcIdx];
		dest.AddedTicks[dstIdx] = AddedTicks[srcIdx];
	}
}

[SkipLocalsInit]
internal struct ArchetypeChunk
{
	internal readonly Column[]? Columns;
	internal readonly ROEntityView[] Entities;
	private readonly World _world;

	internal ArchetypeChunk(World world, ReadOnlySpan<SlimComponent> sign, int chunkSize)
	{
		_world = world;
		Entities = new ROEntityView[chunkSize];
		Columns = new Column[sign.Length];
		for (var i = 0; i < sign.Length; ++i) {
			Columns[i] = new Column(world, in sign[i], chunkSize);
		}
	}

	public int Count { get; internal set; }


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref ROEntityView EntityAt(int row)
		=> ref Unsafe.Add(ref Entities.AsSpan(0, Count)[0], row & Archetype.CHUNK_THRESHOLD);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref T GetReference<T>(int column) 
	{
		if (column < 0 || column >= Columns!.Length)
			return ref Unsafe.NullRef<T>();

		var span = new Span<T>(Unsafe.As<T[]>(Columns[column].Data), 0, Count);
		return ref MemoryMarshal.GetReference(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref T GetReferenceWithSize<T>(int column, out int sizeInBytes) 
	{
		if (column < 0 || column >= Columns!.Length) {
			sizeInBytes = 0;
			return ref Unsafe.NullRef<T>();
		}

		sizeInBytes = Unsafe.SizeOf<T>();

		var span = new Span<T>(Unsafe.As<T[]>(Columns[column].Data), 0, Count);
		return ref MemoryMarshal.GetReference(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref Column GetColumn(int column)
	{
		if (column < 0 || column >= Columns!.Length) {
			return ref Unsafe.NullRef<Column>();
		}

		return ref Columns[column];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ref T GetReferenceAt<T>(int column, int row) 
	{
		ref var reference = ref GetReference<T>(column);
		if (Unsafe.IsNullRef(ref reference))
			return ref reference;
		return ref Unsafe.Add(ref reference, row & Archetype.CHUNK_THRESHOLD);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Span<T> GetSpan<T>(int column) 
	{
		if (column < 0 || column >= Columns!.Length)
			return Span<T>.Empty;

		var span = new Span<T>(Unsafe.As<T[]>(Columns[column].Data), 0, Count);
		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly ReadOnlySpan<ROEntityView> GetEntities()
		=> Entities.AsSpan(0, Count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkChanged(int column, int row, uint ticks)
	{
		Columns![column].MarkChanged(row & Archetype.CHUNK_THRESHOLD, ticks);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkAdded(int column, int row, uint ticks)
	{
		Columns![column].MarkAdded(row & Archetype.CHUNK_THRESHOLD, ticks);
	}
}

[DebuggerDisplay("Archetype(Generation={Generation}, HashId={HashId}, Count={Count}, Components=[{DebuggerDisplayComponentNames()}], Tags=[{Tags.Length}])")]
public sealed class Archetype : IComparable<Archetype>
{
	public static ArchetypeGenerationComparer GenerationComparer = new ArchetypeGenerationComparer();
	private const int ARCHETYPE_INITIAL_CAPACITY = 1;

	internal const int CHUNK_SIZE = 4096;
	internal const int CHUNK_LOG2 = 12;
	internal const int CHUNK_THRESHOLD = CHUNK_SIZE - 1;
	internal readonly List<EcsEdge> _add, _remove;
	private readonly ComponentComparer _comparer;
	private readonly FrozenDictionary<ComponentId, int> _componentsLookup, _allLookup;
	private readonly int[] _fastLookup;


	private string DebuggerDisplayComponentNames()
	{
		return string.Join(", ", Components.Select(c => World.Registry.GetComponent(c.Id).Name));
	}

	public readonly SlimComponent[] All, Components, Tags, Pairs = Array.Empty<SlimComponent>();
	private ArchetypeChunk[] _chunks;

	internal Archetype(
		World world,
		SlimComponent[] sign,
		ComponentComparer comparer,
		ulong generation
	)
	{
		_comparer = comparer;
		World = world;
		All = sign;
		Components = All.Where(x => x.Size > 0).ToArray();
		Tags = All.Where(x => x.Size <= 0).ToArray();
		_chunks = new ArchetypeChunk[ARCHETYPE_INITIAL_CAPACITY];
		Generation = generation;


		var hash = 0ul;
		var dict = new Dictionary<ComponentId, int>();
		var allDict = new Dictionary<ComponentId, int>();
		uint maxId = 0;
		for (int i = 0, cur = 0; i < sign.Length; ++i) {
			hash = UnorderedSetHasher.Combine(hash, sign[i].Id);

			if (sign[i].Size > 0) {
				dict.Add(sign[i].Id, cur++);
				maxId = uint.Max(maxId, sign[i].Id.Id);
			}

			allDict.Add(sign[i].Id, i);
		}

		HashId = hash;

		_fastLookup = new int[maxId + 1];
		_fastLookup.AsSpan().Fill(-1);
		foreach (var (cid, i) in dict) {
			_fastLookup[cid] = i;
		}

		_componentsLookup = dict.ToFrozenDictionary();
		_allLookup = allDict.ToFrozenDictionary();

		_add = new List<EcsEdge>();
		_remove = new List<EcsEdge>();
	}


	public World World { get; }
	public int Count { get; private set; }
	public EcsID HashId { get; }
	public ulong Generation { get; }
	internal ReadOnlySpan<ArchetypeChunk> Chunks => _chunks.AsSpan(0, Count + CHUNK_SIZE - 1 >> CHUNK_LOG2);
	internal int EmptyChunks => _chunks.Length - (Count + CHUNK_SIZE - 1 >> CHUNK_LOG2);

	public override string ToString()
	{
		return
			$"Archetype(Generation: {Generation}, HashId: {HashId}, Count: {Count}, Components: [{string.Join(", ", Components.Select(c => World.Registry.GetComponent(c.Id).Name))}], Tags: [{string.Join(", ", Tags.Select(c => World.Registry.GetComponent(c.Id).Name))}])";
	}

	public int CompareTo(Archetype? other) => HashId.CompareTo(other?.HashId);

	private ref ArchetypeChunk GetOrCreateChunk(int index)
	{
		index >>= CHUNK_LOG2;

		if (index >= _chunks.Length)
			Array.Resize(ref _chunks, Math.Max(ARCHETYPE_INITIAL_CAPACITY, _chunks.Length * 2));

		ref var chunk = ref _chunks[index];
		if (chunk.Columns == null) {
			chunk = new ArchetypeChunk(World, Components, CHUNK_SIZE);
		}

		return ref chunk;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ref ArchetypeChunk GetChunk(int index)
		=> ref _chunks[index >> CHUNK_LOG2];

	internal int GetComponentIndex(EcsID id) => (int)id >= _fastLookup.Length ? -1 : _fastLookup[(int)id];

	internal int GetAnyIndex(ComponentId id) => _allLookup.GetValueOrDefault(id, -1);

	internal bool HasIndex(ComponentId id) => _allLookup.ContainsKey(id);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetComponentIndex<T>() 
	{
		ref readonly var c = ref World.Registry.GetSlimComponent<T>();
		return c.IsTag ? GetComponentIndex(c.Id) : GetAnyIndex(c.Id);
	}

	internal ref ArchetypeChunk Add(ROEntityView ent, out int row)
	{
		ref var chunk = ref GetOrCreateChunk(Count);
		chunk.EntityAt(chunk.Count++) = ent;
		row = Count++;
		return ref chunk;
	}

	internal ref ArchetypeChunk Add(EcsID id, out int newRow)
		=> ref Add(new ROEntityView(World, id), out newRow);

	private EcsID RemoveByRow(ref ArchetypeChunk chunk, int row)
	{
		Count -= 1;
		EcsAssert.Assert(Count >= 0, "Negative count");

		// ref var chunk = ref GetChunk(row);
		ref var lastChunk = ref GetChunk(Count);
		var removed = chunk.EntityAt(row).Id;

		if (row < Count) {
			EcsAssert.Assert(lastChunk.EntityAt(Count).Id.IsValid(), "Entity is invalid. This should never happen!");

			chunk.EntityAt(row) = lastChunk.EntityAt(Count);

			var srcIdx = Count & CHUNK_THRESHOLD;
			var dstIdx = row & CHUNK_THRESHOLD;
			for (var i = 0; i < Components.Length; ++i) {
				lastChunk.Columns![i].CopyTo(srcIdx, in chunk.Columns![i], dstIdx);
				lastChunk.Columns![i].Clear(srcIdx);
			}

			ref var rec = ref World.GetRecord(chunk.EntityAt(row).Id);
			rec.Chunk = chunk;
			rec.Row = row;
		} else {
			// delete the last one
			for (var i = 0; i < Components.Length; ++i) {
				chunk.Columns![i].Clear(row & CHUNK_THRESHOLD);
			}
		}

		// lastChunk.EntityAt(_count) = EntityView.Invalid;
		//
		// for (var i = 0; i < All.Length; ++i)
		// {
		// 	if (All[i].Size <= 0)
		// 		continue;
		//
		// 	var lastValidArray = lastChunk.RawComponentData(i);
		// 	Array.Clear(lastValidArray, _count & CHUNK_THRESHOLD, 1);
		// }

		lastChunk.Count -= 1;
		EcsAssert.Assert(lastChunk.Count >= 0, "Negative chunk count");

		TrimChunksIfNeeded();

		return removed;
	}

	internal EcsID Remove(ref EcsRecord record)
		=> RemoveByRow(ref record.Chunk, record.Row);

	internal Archetype InsertVertex(Archetype left, SlimComponent[] sign, EcsID id, ulong generation)
	{
		var vertex = new Archetype(left.World, sign, _comparer, generation);
		var a = left.All.Length < vertex.All.Length ? left : vertex;
		var b = left.All.Length < vertex.All.Length ? vertex : left;
		MakeEdges(a, b, id);
		InsertVertex(vertex);
		return vertex;
	}

	internal ref ArchetypeChunk MoveEntity(Archetype newArch, ref ArchetypeChunk fromChunk, int oldRow, bool isRemove, out int newRow)
	{
		ref var toChunk = ref newArch.Add(fromChunk.EntityAt(oldRow), out newRow);

		int i = 0, j = 0;
		var count = isRemove ? newArch.Components.Length : Components.Length;

		ref var x = ref isRemove ? ref j : ref i;
		ref var y = ref !isRemove ? ref j : ref i;

		var srcIdx = oldRow & CHUNK_THRESHOLD;
		var dstIdx = newRow & CHUNK_THRESHOLD;
		var items = Components;
		var newItems = newArch.Components;
		for (; x < count; ++x, ++y) {
			while (items[i].Id != newItems[j].Id) {
				// advance the sign with less components!
				++y;
			}

			fromChunk.Columns![i].CopyTo(srcIdx, in toChunk.Columns![j], dstIdx);
		}

		_ = RemoveByRow(ref fromChunk, oldRow);

		return ref toChunk;
	}

	internal void Clear()
	{
		Count = 0;
		_add.Clear();
		_remove.Clear();
		Array.Clear(_chunks, 0, _chunks.Length);
	}

	private void TrimChunksIfNeeded()
	{
		// Cleanup
		var empty = EmptyChunks;
		var half = Math.Max(ARCHETYPE_INITIAL_CAPACITY, _chunks.Length / 2);
		if (empty > half)
			Array.Resize(ref _chunks, half);
	}

	internal void RemoveEmptyArchetypes(ref int removed, ArchetypeRegistry archetypes)
	{
		for (var i = _add.Count - 1; i >= 0; --i) {
			var edge = _add[i];
			edge.Archetype.RemoveEmptyArchetypes(ref removed, archetypes);

			if (edge.Archetype.Count == 0 && edge.Archetype._add.Count == 0) {
				archetypes.Remove(edge.Archetype);
				_remove.Clear();
				_add.RemoveAt(i);

				removed += 1;
			}
		}
	}

	private static void MakeEdges(Archetype left, Archetype right, EcsID id)
	{
		left._add.Add(new EcsEdge { Archetype = right, Id = id });
		right._remove.Add(new EcsEdge { Archetype = left, Id = id });
	}

	private void InsertVertex(Archetype newNode)
	{
		var nodeTypeLen = All.Length;
		var newTypeLen = newNode.All.Length;

		// if (nodeTypeLen > newTypeLen - 1)
		// {
		// 	foreach (ref var edge in CollectionsMarshal.AsSpan(_remove))
		// 	{
		// 		edge.Archetype.InsertVertex(newNode);
		// 	}

		// 	return;
		// }

		if (nodeTypeLen < newTypeLen - 1) {
			foreach (ref var edge in CollectionsMarshal.AsSpan(_add)) {
				edge.Archetype.InsertVertex(newNode);
			}

			return;
		}

		if (!IsSuperset(newNode.All.AsSpan())) {
			return;
		}

		var i = 0;
		var newNodeTypeLen = newNode.All.Length;
		for (; i < newNodeTypeLen && All[i].Id == newNode.All[i].Id; ++i) { }

		MakeEdges(newNode, this, All[i].Id);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsSuperset(ReadOnlySpan<SlimComponent> other)
	{
		int i = 0, j = 0;
		while (i < All.Length && j < other.Length) {
			if (All[i].Id == other[j].Id) {
				j++;
			}

			i++;
		}

		return j == other.Length;
	}

	internal Archetype? TraverseLeft(EcsID nodeId)
		=> Traverse(this, nodeId, false);

	internal Archetype? TraverseRight(EcsID nodeId)
		=> Traverse(this, nodeId, true);

	private static Archetype? Traverse(Archetype root, EcsID nodeId, bool onAdd)
	{
		foreach (ref var edge in CollectionsMarshal.AsSpan(onAdd ? root._add : root._remove)) {
			if (edge.Id == nodeId)
				return edge.Archetype;
		}

		// foreach (ref var edge in CollectionsMarshal.AsSpan(onAdd ? root._add : root._remove))
		// {
		// 	var found = onAdd ? edge.Archetype.TraverseRight(nodeId) : edge.Archetype.TraverseLeft(nodeId);
		// 	if (found != null)
		// 		return found;
		// }

		return null;
	}

	internal void GetSuperSets(ReadOnlySpan<IQueryTerm> terms, List<Archetype> matched)
	{
		var result = MatchWith(terms);
		if (result == ArchetypeSearchResult.Stop) {
			return;
		}

		if (result == ArchetypeSearchResult.Found) {
			matched.Add(this);
		}

		var add = _add;
		if (add.Count <= 0)
			return;

		foreach (ref var edge in CollectionsMarshal.AsSpan(add)) {
			edge.Archetype.GetSuperSets(terms, matched);
		}
	}

	internal ArchetypeSearchResult MatchWith(ReadOnlySpan<IQueryTerm> terms) => FilterMatch.Match(this, terms);

	public void Print(int depth)
	{
		Console.WriteLine(new string(' ', depth * 2) + $"Node: [{string.Join(", ", All.Select(s => s.Id))}]");

		foreach (ref var edge in CollectionsMarshal.AsSpan(_add)) {
			Console.WriteLine(new string(' ', (depth + 1) * 2) + $"Edge: {edge.Id}");
			edge.Archetype.Print(depth + 2);
		}
	}


}

public class ArchetypeGenerationComparer : IComparer<Archetype>
{

	public int Compare(Archetype? x, Archetype? y)
	{
		if (ReferenceEquals(x, y)) return 0;
		if (y is null) return 1;
		if (x is null) return -1;
		return x.Generation.CompareTo(y.Generation);
	}
}

internal struct EcsEdge
{
	public EcsID Id;
	public Archetype Archetype;
}