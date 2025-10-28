using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Verse.ECS.Datastructures;

public class IndexDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
	where TKey : notnull
{
	private readonly Dictionary<TKey, int> _keyToIndex;
	private readonly List<KeyValuePair<TKey, TValue>> _entries;

	public IndexDictionary()
	{
		_keyToIndex = new Dictionary<TKey, int>();
		_entries = new List<KeyValuePair<TKey, TValue>>();
	}

	public IndexDictionary(int capacity)
	{
		_keyToIndex = new Dictionary<TKey, int>(capacity);
		_entries = new List<KeyValuePair<TKey, TValue>>(capacity);
	}

	public IndexDictionary(IEqualityComparer<TKey> comparer)
	{
		_keyToIndex = new Dictionary<TKey, int>(comparer);
		_entries = new List<KeyValuePair<TKey, TValue>>();
	}

	public IndexDictionary(int capacity, IEqualityComparer<TKey> comparer)
	{
		_keyToIndex = new Dictionary<TKey, int>(capacity, comparer);
		_entries = new List<KeyValuePair<TKey, TValue>>(capacity);
	}

	public int Count => _entries.Count;
	public bool IsReadOnly => false;

	public ICollection<TKey> Keys => _entries.Select(kvp => kvp.Key).ToList();
	public ICollection<TValue> Values => _entries.Select(kvp => kvp.Value).ToList();
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

	public TValue this[TKey key] {
		get {
			if (!_keyToIndex.TryGetValue(key, out int index))
				throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
			return _entries[index].Value;
		}
		set {
			if (_keyToIndex.TryGetValue(key, out int index)) {
				_entries[index] = new KeyValuePair<TKey, TValue>(key, value);
			} else {
				Add(key, value);
			}
		}
	}

	public TValue this[int index] {
		get {
			if (index < 0 || index >= _entries.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			return _entries[index].Value;
		}
		set {
			if (index < 0 || index >= _entries.Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			var key = _entries[index].Key;
			_entries[index] = new KeyValuePair<TKey, TValue>(key, value);
		}
	}

	public void Add(TKey key, TValue value)
	{
		if (_keyToIndex.ContainsKey(key))
			throw new ArgumentException($"An item with the same key has already been added. Key: {key}");

		int index = _entries.Count;
		_keyToIndex[key] = index;
		_entries.Add(new KeyValuePair<TKey, TValue>(key, value));
	}

	public (int Index, TValue? Value) AddOrReplace(TKey key, TValue value, out bool replaced)
	{
		if (_keyToIndex.TryGetValue(key, out int index)) {
			var entry = _entries[index];
			_entries[index] = new KeyValuePair<TKey, TValue>(key, value);
			replaced = true;
			return (index, entry.Value);
		}
		index = _entries.Count;
		_keyToIndex[key] = index;
		_entries.Add(new KeyValuePair<TKey, TValue>(key, value));
		replaced = false;
		return (index, default);
	}

	public void Add(KeyValuePair<TKey, TValue> item)
	{
		Add(item.Key, item.Value);
	}

	public bool Remove(TKey key)
	{
		if (!_keyToIndex.TryGetValue(key, out int index))
			return false;

		_keyToIndex.Remove(key);
		_entries.RemoveAt(index);

		for (int i = index; i < _entries.Count; i++) {
			_keyToIndex[_entries[i].Key] = i;
		}

		return true;
	}

	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		if (!_keyToIndex.TryGetValue(item.Key, out int index))
			return false;

		var entry = _entries[index];
		if (!EqualityComparer<TValue>.Default.Equals(entry.Value, item.Value))
			return false;

		return Remove(item.Key);
	}

	public (TKey, TValue)? SwapRemoveIndex(int index)
	{
		if (index < 0 || index >= _entries.Count)
			throw new ArgumentOutOfRangeException(nameof(index));
		return SwapRemove(_entries[index].Key);
	}

	public (TKey, TValue)? SwapRemove(TKey key)
	{
		if (!_keyToIndex.TryGetValue(key, out int index))
			return null;

		_keyToIndex.Remove(key);
		var value = _entries[index].Value;

		if (index == _entries.Count - 1) {
			_entries.RemoveAt(index);
		} else {
			var lastEntry = _entries[_entries.Count - 1];
			_entries[index] = lastEntry;
			_entries.RemoveAt(_entries.Count - 1);
			_keyToIndex[lastEntry.Key] = index;
		}

		return (key, value);
	}

	public bool ContainsKey(TKey key)
	{
		return _keyToIndex.ContainsKey(key);
	}

	public bool Contains(KeyValuePair<TKey, TValue> item)
	{
		if (!_keyToIndex.TryGetValue(item.Key, out int index))
			return false;

		var entry = _entries[index];
		return EqualityComparer<TValue>.Default.Equals(entry.Value, item.Value);
	}

	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		if (_keyToIndex.TryGetValue(key, out int index)) {
			value = _entries[index].Value;
			return true;
		}

		value = default;
		return false;
	}

	public void Clear()
	{
		_keyToIndex.Clear();
		_entries.Clear();
	}

	public int GetIndex(TKey key)
	{
		if (!_keyToIndex.TryGetValue(key, out int index))
			throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
		return index;
	}

	public bool TryGetIndex(TKey key, out int index)
	{
		return _keyToIndex.TryGetValue(key, out index);
	}

	public KeyValuePair<TKey, TValue> GetByIndex(int index)
	{
		if (index < 0 || index >= _entries.Count)
			throw new ArgumentOutOfRangeException(nameof(index));
		return _entries[index];
	}

	public TKey GetKeyByIndex(int index)
	{
		if (index < 0 || index >= _entries.Count)
			throw new ArgumentOutOfRangeException(nameof(index));
		return _entries[index].Key;
	}

	public TValue GetValueByIndex(int index)
	{
		if (index < 0 || index >= _entries.Count)
			throw new ArgumentOutOfRangeException(nameof(index));
		return _entries[index].Value;
	}

	public (TKey Key, TValue Value, int Index) GetFull(TKey key)
	{
		if (!_keyToIndex.TryGetValue(key, out int index))
			throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
		var entry = _entries[index];
		return (entry.Key, entry.Value, index);
	}

	public bool TryGetFull(TKey key, out TValue value, out int index)
	{
		if (_keyToIndex.TryGetValue(key, out index)) {
			var entry = _entries[index];
			value = entry.Value;
			return true;
		}

		value = default!;
		index = -1;
		return false;
	}

	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		if (array == null)
			throw new ArgumentNullException(nameof(array));
		if (arrayIndex < 0)
			throw new ArgumentOutOfRangeException(nameof(arrayIndex));
		if (array.Length - arrayIndex < Count)
			throw new ArgumentException(
				"The number of elements in the source IndexDictionary is greater than the available space from arrayIndex to the end of the destination array.");

		for (int i = 0; i < _entries.Count; i++) {
			array[arrayIndex + i] = _entries[i];
		}
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
	{
		return _entries.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}