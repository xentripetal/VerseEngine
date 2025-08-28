using System.Diagnostics.CodeAnalysis;

namespace Verse.ECS;

/// <summary>
///     Used in query filters to find entities with the corrisponding component/tag.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct With<T> : IFilter<With<T>>
	where T : struct
{
	[UnscopedRef]
	ref With<T> IQueryIterator<With<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.With<T>();
	}

	static With<T> IFilter<With<T>>.CreateIterator(QueryIterator iterator) => new With<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly With<T> IQueryIterator<With<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly bool IQueryIterator<With<T>>.MoveNext() => true;

	public readonly void SetTicks(uint lastRun, uint thisRun) { }
}

/// <summary>
///     Used in query filters to find entities without the corrisponding component/tag.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Without<T> : IFilter<Without<T>>
	where T : struct
{
	[UnscopedRef]
	ref Without<T> IQueryIterator<Without<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.Without<T>();
	}

	static Without<T> IFilter<Without<T>>.CreateIterator(QueryIterator iterator) => new Without<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly Without<T> IQueryIterator<Without<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly bool IQueryIterator<Without<T>>.MoveNext() => true;

	public readonly void SetTicks(uint lastRun, uint thisRun) { }
}

/// <summary>
///     Used in query filters to find entities with or without the corrisponding component/tag.<br />
///     You would Unsafe.IsNullRef&lt;T&gt;(); to check if the value has been found.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Optional<T> : IFilter<Optional<T>>
	where T : struct
{
	[UnscopedRef]
	ref Optional<T> IQueryIterator<Optional<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.Optional<T>();
	}

	static Optional<T> IFilter<Optional<T>>.CreateIterator(QueryIterator iterator) => new Optional<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly Optional<T> IQueryIterator<Optional<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly bool IQueryIterator<Optional<T>>.MoveNext() => true;

	public readonly void SetTicks(uint lastRun, uint thisRun) { }
}

/// <summary>
///     Used in query filters to find entities with components that have changed.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Changed<T> : IFilter<Changed<T>>
	where T : struct
{
	private QueryIterator _iterator;
	private Ptr<uint> _stateRow;
	private int _row, _count;
	private nint _size;
	private uint _lastRun, _thisRun;

	private Changed(QueryIterator iterator)
	{
		_iterator = iterator;
		_row = -1;
		_count = -1;
		_lastRun = 0;
		_thisRun = 0;
	}

	[UnscopedRef]
	ref Changed<T> IQueryIterator<Changed<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.With<T>();
	}

	static Changed<T> IFilter<Changed<T>>.CreateIterator(QueryIterator iterator) => new Changed<T>(iterator);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly Changed<T> IQueryIterator<Changed<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	bool IQueryIterator<Changed<T>>.MoveNext()
	{
		if (++_row >= _count) {
			if (!_iterator.Next())
				return false;

			_row = 0;
			_count = _iterator.Count;
			var index = _iterator.GetColumnIndexOf<T>();
			var states = _iterator.GetChangedTicks(index);

			if (states.IsEmpty) {
				_stateRow.Value = ref Unsafe.NullRef<uint>();
				_size = 0;
			} else {
				_stateRow.Value = ref MemoryMarshal.GetReference(states);
				_size = Unsafe.SizeOf<uint>();
			}
		} else {
			_stateRow.Value = ref Unsafe.AddByteOffset(ref _stateRow.Value, _size);
		}

		// TODO why did tinyecs filter for < thisRun?
		return _size > 0 && _stateRow.Value > _lastRun; //&& _stateRow.Value < _thisRun;
	}

	public void SetTicks(uint lastRun, uint thisRun)
	{
		_lastRun = lastRun;
		_thisRun = thisRun;
	}
}

/// <summary>
///     Used in query filters to find entities with components that have added.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Added<T> : IFilter<Added<T>>
	where T : struct
{
	private QueryIterator _iterator;
	private Ptr<uint> _stateRow;
	private int _row, _count;
	private nint _size;
	private uint _lastRun, _thisRun;

	private Added(QueryIterator iterator)
	{
		_iterator = iterator;
		_row = -1;
		_count = -1;
		_lastRun = 0;
		_thisRun = 0;
	}

	[UnscopedRef]
	ref Added<T> IQueryIterator<Added<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.With<T>();
	}

	static Added<T> IFilter<Added<T>>.CreateIterator(QueryIterator iterator) => new Added<T>(iterator);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly Added<T> IQueryIterator<Added<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	bool IQueryIterator<Added<T>>.MoveNext()
	{
		if (++_row >= _count) {
			if (!_iterator.Next())
				return false;

			_row = 0;
			_count = _iterator.Count;
			var index = _iterator.GetColumnIndexOf<T>();
			var states = _iterator.GetAddedTicks(index);

			if (states.IsEmpty) {
				_stateRow.Value = ref Unsafe.NullRef<uint>();
				_size = 0;
			} else {
				_stateRow.Value = ref MemoryMarshal.GetReference(states);
				_size = Unsafe.SizeOf<uint>();
			}
		} else {
			_stateRow.Value = ref Unsafe.AddByteOffset(ref _stateRow.Value, _size);
		}

		return _size > 0 && _stateRow.Value >= _lastRun && _stateRow.Value < _thisRun;
	}

	public void SetTicks(uint lastRun, uint thisRun)
	{
		_lastRun = lastRun;
		_thisRun = thisRun;
	}
}

public ref struct Writes<T> : IFilter<Writes<T>>
	where T : struct
{
	[UnscopedRef]
	ref Writes<T> IQueryIterator<Writes<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		builder.With<T>(TermAccess.Write);
	}

	static Writes<T> IFilter<Writes<T>>.CreateIterator(QueryIterator iterator) => new Writes<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly Writes<T> IQueryIterator<Writes<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly bool IQueryIterator<Writes<T>>.MoveNext() => true;

	public readonly void SetTicks(uint lastRun, uint thisRun) { }
}

public ref struct MarkChanged<T> : IFilter<MarkChanged<T>>
	where T : struct
{
	private QueryIterator _iterator;
	private Ptr<uint> _stateRow;
	private int _row, _count;
	private nint _size;
	private uint _lastRun, _thisRun;

	private MarkChanged(QueryIterator iterator)
	{
		_iterator = iterator;
		_row = -1;
		_count = -1;
		_lastRun = 0;
		_thisRun = 0;
	}

	[UnscopedRef]
	ref MarkChanged<T> IQueryIterator<MarkChanged<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
		// builder.With<T>();
	}

	static MarkChanged<T> IFilter<MarkChanged<T>>.CreateIterator(QueryIterator iterator) => new MarkChanged<T>(iterator);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly MarkChanged<T> IQueryIterator<MarkChanged<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	bool IQueryIterator<MarkChanged<T>>.MoveNext()
	{
		if (++_row >= _count) {
			if (!_iterator.Next())
				return false;

			_row = 0;
			_count = _iterator.Count;
			var index = _iterator.GetColumnIndexOf<T>();
			var states = _iterator.GetChangedTicks(index);

			if (states.IsEmpty) {
				_stateRow.Value = ref Unsafe.NullRef<uint>();
				_size = 0;
			} else {
				_stateRow.Value = ref MemoryMarshal.GetReference(states);
				_size = Unsafe.SizeOf<uint>();
			}
		} else {
			_stateRow.Value = ref Unsafe.AddByteOffset(ref _stateRow.Value, _size);
		}

		if (_size > 0) {
			_stateRow.Value = _thisRun;
		}

		return true;
	}

	public void SetTicks(uint lastRun, uint thisRun)
	{
		_lastRun = lastRun;
		_thisRun = thisRun;
	}
}

public partial struct Parent { }
public partial interface IChildrenComponent { }

[SkipLocalsInit]
public ref struct QueryIter<D, F>
	where D : struct, IData<D>, allows ref struct
	where F : struct, IFilter<F>, allows ref struct
{
	private D _dataIterator;
	private F _filterIterator;

	internal QueryIter(uint lastRun, uint thisRun, QueryIterator iterator)
	{
		_dataIterator = D.CreateIterator(iterator);
		_filterIterator = F.CreateIterator(iterator);
		_filterIterator.SetTicks(lastRun, thisRun);
	}

	[UnscopedRef]
	public ref D Current => ref _dataIterator;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool MoveNext()
	{
		while (true) {
			if (!_dataIterator.MoveNext())
				return false;

			if (!_filterIterator.MoveNext())
				continue;

			return true;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly QueryIter<D, F> GetEnumerator() => this;
}