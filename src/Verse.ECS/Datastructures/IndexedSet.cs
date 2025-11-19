using System.Collections;

namespace Verse.ECS.Datastructures;

[DebuggerDisplay("Count = {Count}")]
public class IndexSet<T> : ISet<T>, IReadOnlyOrderedSet<T> where T : notnull
{
	private readonly List<T> _items;
	private readonly Dictionary<T, int> _indexMap;

	public IndexSet(int capacity = 0, IEqualityComparer<T>? comparer = null)
	{
		_items = new List<T>(capacity);
		_indexMap = new Dictionary<T, int>(capacity, comparer);
	}

	public int Count => _items.Count;
	public bool IsReadOnly => false;

	public T this[int index] {
		get => _items[index];
		set {
			if (index < 0 || index >= _items.Count)
				throw new ArgumentOutOfRangeException(nameof(index));

			var item = _items[index];
			_indexMap.Remove(item);
			_indexMap[value] = index;
			_items[index] = value;
		}
	}

	public bool Add(T item)
	{
		if (_indexMap.ContainsKey(item))
			return false;

		_indexMap[item] = _items.Count;
		_items.Add(item);
		return true;
	}

	void ICollection<T>.Add(T item) => Add(item);

	public bool Remove(T item)
	{
		if (!_indexMap.TryGetValue(item, out int index))
			return false;

		RemoveAt(index);
		return true;
	}
	
	public void RemoveAt(int index)
	{
		if (index < 0 || index >= _items.Count)
			throw new ArgumentOutOfRangeException(nameof(index));

		var item = _items[index];
		_indexMap.Remove(item);
		_items.RemoveAt(index);

		for (int i = index; i < _items.Count; i++) {
			_indexMap[_items[i]] = i;
		}
	}

	public void Clear()
	{
		_items.Clear();
		_indexMap.Clear();
	}

	public bool Contains(T item) => _indexMap.ContainsKey(item);

	public int IndexOf(T item)
	{
		return _indexMap.TryGetValue(item, out int index) ? index : -1;
	}
	public bool TryGetIndexOf(T Item, out int index)
	{
		return _indexMap.TryGetValue(Item, out index);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		_items.CopyTo(array, arrayIndex);
	}

	public void ExceptWith(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		foreach (var item in other) {
			Remove(item);
		}
	}

	public void IntersectWith(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		var otherSet = new HashSet<T>(other);
		var toRemove = new List<T>();

		foreach (var item in _items) {
			if (!otherSet.Contains(item))
				toRemove.Add(item);
		}

		foreach (var item in toRemove) {
			Remove(item);
		}
	}

	public bool IsProperSubsetOf(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		var otherSet = new HashSet<T>(other);
		return Count < otherSet.Count && IsSubsetOf(otherSet);
	}

	public bool IsProperSupersetOf(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		var otherSet = new HashSet<T>(other);
		return Count > otherSet.Count && IsSupersetOf(otherSet);
	}

	public bool IsSubsetOf(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		var otherSet = new HashSet<T>(other);
		return _items.All(item => otherSet.Contains(item));
	}

	public bool IsSupersetOf(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		return other.All(item => Contains(item));
	}

	public bool Overlaps(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		return other.Any(item => Contains(item));
	}

	public bool SetEquals(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		var otherSet = new HashSet<T>(other);
		return Count == otherSet.Count && _items.All(item => otherSet.Contains(item));
	}

	public void SymmetricExceptWith(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		foreach (var item in other) {
			if (!Remove(item)) {
				Add(item);
			}
		}
	}

	public void UnionWith(IEnumerable<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);

		foreach (var item in other) {
			Add(item);
		}
	}

	public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public void AddRange(IEnumerable<T> elements) 
	{
		foreach (var element in elements) {
			Add(element);
		}
	}
}