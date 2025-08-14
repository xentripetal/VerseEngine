namespace Verse.ECS;

internal sealed class FastIdLookup<TValue> where TValue : notnull
{
	private const int COMPONENT_MAX_ID = 1024;

	private readonly Dictionary<ulong, TValue> _slowLookup = new Dictionary<ulong, TValue>();
	private readonly TValue[] _fastLookup = new TValue[COMPONENT_MAX_ID];
	private readonly bool[] _fastLookupAdded = new bool[COMPONENT_MAX_ID];

	public int Count => _slowLookup.Count + CountFastLookup();

	public void Add(ulong id, TValue value)
	{
		if (id < COMPONENT_MAX_ID) {
			_fastLookup[id] = value;
			_fastLookupAdded[id] = true;
		} else {
#if NET
			CollectionsMarshal.GetValueRefOrAddDefault(_slowLookup, id, out _) = value;
#else
            _slowLookup.GetOrAddValueRef(id, out _) = value;
#endif
		}
	}

	public ref TValue GetOrCreate(ulong id, out bool exists)
	{
		if (id < COMPONENT_MAX_ID) {
			if (_fastLookupAdded[id]) {
				exists = true;
				return ref _fastLookup[id];
			}

			exists = false;
			return ref AddToFast(id);
		}

		ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(_slowLookup, id, out exists);
		return ref val;
	}


	public ref TValue TryGet(ulong id, out bool exists)
	{
		if (id < COMPONENT_MAX_ID) {
			if (_fastLookupAdded[id]) {
				exists = true;
				return ref _fastLookup[id];
			}

			exists = false;
			return ref Unsafe.NullRef<TValue>();
		}

		ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(_slowLookup, id);
		exists = !Unsafe.IsNullRef(ref val);
		return ref val;
	}

	public bool ContainsKey(ulong id)
	{
		if (id < COMPONENT_MAX_ID)
			return _fastLookupAdded[id];
		return _slowLookup.ContainsKey(id);
	}

	public void Clear()
	{
		Array.Clear(_fastLookup, 0, _fastLookup.Length);
		Array.Fill(_fastLookupAdded, false);
		_slowLookup.Clear();
	}


	private ref TValue AddToFast(ulong id)
	{
		ref var value = ref _fastLookup[id];
		_fastLookupAdded[id] = true;
		return ref value;
	}

	private int CountFastLookup()
	{
		var count = 0;
		for (var i = 0; i < _fastLookupAdded.Length; i++) {
			if (_fastLookupAdded[i])
				count++;
		}
		return count;
	}

	public IEnumerator<KeyValuePair<ulong, TValue>> GetEnumerator()
	{
		foreach (var pair in _slowLookup) {
			yield return pair;
		}

		for (ulong i = 0; i < COMPONENT_MAX_ID; i++) {
			if (_fastLookupAdded[i])
				yield return new KeyValuePair<ulong, TValue>(i, _fastLookup[i]);
		}
	}
	public ref TValue this[EcsID id] => ref TryGet(id, out var _);
}