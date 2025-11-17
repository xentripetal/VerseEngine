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

	public readonly void SetTicks(Tick lastRun, Tick thisRun) { }
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

	public readonly void SetTicks(Tick lastRun, Tick thisRun) { }
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

	public readonly void SetTicks(Tick lastRun, Tick thisRun) { }
}

/// <summary>
///     Used in query filters to find entities with components that have changed.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Changed<T> : IFilter<Changed<T>>
	where T : struct
{
	private QueryIterator iterator;
	private Ptr<Tick> changeTick;
	private int row, count;
	private nint size;

	private Changed(QueryIterator iterator)
	{
		this.iterator = iterator;
		row = -1;
		count = -1;
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
		if (++row >= count) {
			if (!iterator.Next())
				return false;

			row = 0;
			count = iterator.Count;
			var index = iterator.GetColumnIndexOf<T>();
			var states = iterator.GetChangedTicks(index);

			if (states.IsEmpty) {
				changeTick.Value = ref Unsafe.NullRef<Tick>();
				size = 0;
			} else {
				changeTick.Value = ref MemoryMarshal.GetReference(states);
				size = Unsafe.SizeOf<Tick>();
			}
		} else {
			changeTick.Value = ref Unsafe.AddByteOffset(ref changeTick.Value, size);
		}

		// TODO why did tinyecs filter for < thisRun?
		return size > 0 && changeTick.Value.IsNewerThan(iterator.LastRun, iterator.ThisRun);
	}
}

/// <summary>
///     Used in query filters to find entities with components that have added.
/// </summary>
/// <typeparam name="T"></typeparam>
public ref struct Added<T> : IFilter<Added<T>>
	where T : struct
{
	private QueryIterator iterator;
	private Cell<Tick> stateRow;
	private int row, count;
	private nint size;

	private Added(QueryIterator iterator)
	{
		this.iterator = iterator;
		row = -1;
		count = -1;
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
		// TODO can we use the DataRow{T} from our parent query instead of re-iterating?
		if (++row >= count) {
			if (!iterator.Next())
				return false;

			row = 0;
			count = iterator.Count;
			var index = iterator.GetColumnIndexOf<T>();
			var states = iterator.GetAddedTicks(index);

			if (states.IsEmpty) {
				stateRow.Value = ref Unsafe.NullRef<Tick>();
				size = 0;
			} else {
				stateRow.Value = ref MemoryMarshal.GetReference(states);
				size = Unsafe.SizeOf<uint>();
			}
		} else {
			stateRow.Value = ref Unsafe.AddByteOffset(ref stateRow.Value, size);
		}

		return size > 0 && stateRow.Value.IsNewerThan(iterator.LastRun, iterator.ThisRun);
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
}

public ref struct MarkChanged<T> : IFilter<MarkChanged<T>>
	where T : struct
{
	private QueryIterator iterator;
	private Ptr<Tick> stateRow;
	private int row, count;
	private nint size;

	private MarkChanged(QueryIterator iterator)
	{
		this.iterator = iterator;
		row = -1;
		count = -1;
	}

	[UnscopedRef]
	ref MarkChanged<T> IQueryIterator<MarkChanged<T>>.Current => ref this;

	public static void Build(QueryBuilder builder)
	{
	}

	static MarkChanged<T> IFilter<MarkChanged<T>>.CreateIterator(QueryIterator iterator) => new MarkChanged<T>(iterator);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	readonly MarkChanged<T> IQueryIterator<MarkChanged<T>>.GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	bool IQueryIterator<MarkChanged<T>>.MoveNext()
	{
		if (++row >= count) {
			if (!iterator.Next())
				return false;

			row = 0;
			count = iterator.Count;
			var index = iterator.GetColumnIndexOf<T>();
			var states = iterator.GetChangedTicks(index);

			if (states.IsEmpty) {
				stateRow.Value = ref Unsafe.NullRef<Tick>();
				size = 0;
			} else {
				stateRow.Value = ref MemoryMarshal.GetReference(states);
				size = Unsafe.SizeOf<Tick>();
			}
		} else {
			stateRow.Value = ref Unsafe.AddByteOffset(ref stateRow.Value, size);
		}

		if (size > 0) {
			stateRow.Value = iterator.ThisRun;
		}

		return true;
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

	internal QueryIter(QueryIterator iterator)
	{
		_dataIterator = D.CreateIterator(iterator);
		_filterIterator = F.CreateIterator(iterator);
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