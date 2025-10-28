using System.Collections;

namespace Verse.ECS;

public class ReadOnlySparseArray<I, V>(V?[] items)
	where I : ISparseSetIndex<I>
{
	public bool Contains(I index)
	{
		var idx = index.SparseSetIndex();
		return idx >= 0 && idx < items.Length && items[idx] != null;
	}

	public V? Get(I index)
	{
		var idx = index.SparseSetIndex();
		if (idx >= 0 && idx < items.Length) {
			return items[idx];
		}
		return default;
	}
}

public class SparseArray<I, V>
	where I : ISparseSetIndex<I>
{
	private V?[] items;

	public SparseArray()
	{
		items = Array.Empty<V?>();
	}

	public SparseArray(int capacity)
	{
		items = new V?[capacity];
	}

	public bool Contains(I index)
	{
		var idx = index.SparseSetIndex();
		return idx >= 0 && idx < items.Length && items[idx] != null;
	}

	public V? Get(I index)
	{
		var idx = index.SparseSetIndex();
		if (idx >= 0 && idx < items.Length) {
			return items[idx];
		}
		return default;
	}

	/// <summary>
	/// Inserts <paramref name="value"/> at <paramref name="index"/>, expanding the internal storage as necessary.
	/// </summary>
	/// <param name="index">Index of element to insert</param>
	/// <param name="value">Value of element</param>
	public void Insert(I index, V value)
	{
		var idx = index.SparseSetIndex();
		if (idx >= items.Length) {
			var toAdd = idx - items.Length + 1;
			Array.Resize(ref items, items.Length + toAdd);
		}
		items[idx] = value;
	}

	public ref V GetRef(I index)
	{
		var idx = index.SparseSetIndex();
		if (idx >= 0 && idx < items.Length && items[idx] != null)
			return ref items[idx]!;
		return ref Unsafe.NullRef<V>()!;
	}

	public V? Remove(I index)
	{
		var idx = index.SparseSetIndex();
		if (idx >= 0 && idx < items.Length) {
			var value = items[idx];
			items[idx] = default;
			return value;
		}
		return default;
	}

	public void Clear()
	{
		Array.Clear(items, 0, items.Length);
	}

	public ReadOnlySparseArray<I, V> AsReadOnly()
	{
		return new ReadOnlySparseArray<I, V>(items);
	}
}

public class FastList<T> : IEnumerable<T>
{
	private T[] items;
	private int count;

	public FastList(int capacity = 4)
	{
		items = new T[capacity];
		count = 0;
	}

	public int Count => count;

	public T this[int index]
	{
		get => items[index];
		set => items[index] = value;
	}

	public void Add(T item)
	{
		if (count >= items.Length) {
			Array.Resize(ref items, Math.Max(items.Length*2, 1));
		}
		items[count++] = item;
	}

	public void Clear()
	{
		Array.Clear(items, 0, count);
		count = 0;
	}

	public ref T GetRef(int index)
	{
		return ref items[index];
	}
	
	public int Capacity => items.Length;


	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < count; i++) {
			yield return items[i];
		}
	}
	
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public T SwapRemove(int denseIndex)
	{
		var lastIndex = count - 1;
		var value = items[denseIndex];
		items[denseIndex] = items[lastIndex];
		items[lastIndex] = default!;
		count--;
		return value;
	}
}


/// <summary>
/// A value that is usable as an index in a <see cref="SparseSet{I,V}"/> or <see cref="SparseArray{I,V}"/>
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface ISparseSetIndex<out TSelf>
{
	/// <summary>
	/// Gets the sparse set index corresponding to this value.
	/// </summary>
	/// <returns></returns>
	int SparseSetIndex();

	/// <summary>
	/// Creates a new instance of this type with the specified index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	static abstract TSelf GetSparseSetIndex(int index);
}

public struct IntSparseSetIndex(int index) : ISparseSetIndex<IntSparseSetIndex>
{
	public int SparseSetIndex()
	{
		return index;
	}

	public static IntSparseSetIndex GetSparseSetIndex(int index)
	{
		return new IntSparseSetIndex(index);
	}
}

public class SparseSet<I, V> : IEnumerable<KeyValuePair<I, V>>
	where I : ISparseSetIndex<I>
{
	private FastList<V> dense;
	private FastList<I> indices;
	private SparseArray<I, int?> sparse;

	public SparseSet() : this(0) { }

	public SparseSet(int capacity)
	{
		dense = new FastList<V>(capacity);
		indices = new FastList<I>(capacity);
		sparse = new SparseArray<I, int?>(capacity);
	}
	
	public int Capacity => dense.Capacity;
	
	public int Length => dense.Count;

	public bool Contains(I index)
	{
		return sparse.Contains(index);
	}

	public V? Get(I index)
	{
		var pos = sparse.Get(index);
		if (pos.HasValue) {
			return dense[pos.Value];
		}
		return default;
	}
	
	public ref V? GetRef(I index)
	{
		var pos = sparse.Get(index);
		if (pos.HasValue) {
			return ref dense.GetRef(pos.Value)!;
		}
		return ref Unsafe.NullRef<V>()!;
	}

	public IEnumerable<I> Indices => indices.AsEnumerable();

	public IEnumerable<V> Values => dense.AsEnumerable();

	public IEnumerator<KeyValuePair<I, V>> GetEnumerator()
	{
		for (int i = 0; i < dense.Count; i++) {
			yield return new KeyValuePair<I, V>(indices[i], dense[i]);
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void Insert(I index, V value)
	{
		var existingDenseIndex = sparse.Get(index);
		if (existingDenseIndex.HasValue) {
			dense[existingDenseIndex.Value] = value;
		} else {
			sparse.Insert(index, dense.Count);
			indices.Add(index);
			dense.Add(value);
		}
	}

	public ref V GetOrInsert(I index, Func<V> fn)
	{
		var existingDenseIndex = sparse.Get(index);
		if (existingDenseIndex.HasValue) {
			return ref dense.GetRef(existingDenseIndex.Value);
		}
		var value = fn();
		var denseIndex = dense.Count;
		sparse.Insert(index, denseIndex);
		indices.Add(index);
		dense.Add(value);
		return ref dense.GetRef(denseIndex);
	}
	
	public bool IsEmpty()
	{
		return dense.Count == 0;
	}

	public V? Remove(I index)
	{
		var denseIndex = sparse.Remove(index);
		if (denseIndex.HasValue) {
			var isLast = denseIndex.Value == dense.Count - 1;
			var value = dense.SwapRemove(denseIndex.Value);
			indices.SwapRemove(denseIndex.Value);
			if (!isLast) {
				var swappedIndex = indices[denseIndex.Value];
				ref var prevIndex = ref sparse.GetRef(swappedIndex);
				prevIndex = denseIndex.Value;
			}
			return value;
		}
		return default;
	}

	public void Clear()
	{
		dense.Clear();
		indices.Clear();
		sparse.Clear();
	}

	public ReadOnlySparseSet<I, V> AsReadOnly()
	{
		return new ReadOnlySparseSet<I, V>(this);
	}
}

public class ReadOnlySparseSet<I, V> : IEnumerable<KeyValuePair<I, V>>
	where I : ISparseSetIndex<I>
{
	private readonly SparseSet<I, V> _sparseSet;

	public ReadOnlySparseSet(SparseSet<I, V> sparseSet)
	{
		_sparseSet = sparseSet;
	}

	public int Length => _sparseSet.Length;

	public bool Contains(I index)
	{
		return _sparseSet.Contains(index);
	}

	public V? Get(I index)
	{
		return _sparseSet.Get(index);
	}

	public IEnumerable<I> Indices => _sparseSet.Indices;

	public IEnumerable<V> Values => _sparseSet.Values;

	public IEnumerator<KeyValuePair<I, V>> GetEnumerator()
	{
		return _sparseSet.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// TODO Component Sparse Sets for storing certain components