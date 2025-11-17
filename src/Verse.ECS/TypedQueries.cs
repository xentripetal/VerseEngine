using System.Diagnostics.CodeAnalysis;

namespace Verse.ECS;

public interface ITermCreator
{
	public static abstract void Build(QueryBuilder builder);
}

public interface IQueryIterator<TData>
	where TData : struct, allows ref struct
{

	[UnscopedRef]
	ref TData Current { get; }
	TData GetEnumerator();
	
	bool MoveNext();
}

public interface IData<TData> : ITermCreator, IQueryIterator<TData>
	where TData : struct, allows ref struct
{
	public static abstract TData CreateIterator(QueryIterator iterator);
}

public interface IFilter<TFilter> : ITermCreator, IQueryIterator<TFilter>
	where TFilter : struct, allows ref struct
{
	public static abstract TFilter CreateIterator(QueryIterator iterator);
}

public ref struct Empty : IData<Empty>, IFilter<Empty>
{
	private readonly bool _asFilter;
	private QueryIterator _iterator;

	internal Empty(QueryIterator iterator, bool asFilter)
	{
		_iterator = iterator;
		_asFilter = asFilter;
	}

	public static void Build(QueryBuilder builder) { }


	[UnscopedRef]
	public ref Empty Current => ref this;

	public readonly void Deconstruct(out ReadOnlySpan<ROEntityView> entities, out int count)
	{
		entities = _iterator.Entities();
		count = entities.Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Empty GetEnumerator() => this;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool MoveNext() => _asFilter || _iterator.Next();

	public readonly void SetTicks(Tick lastRun, Tick thisRun) { }

	static Empty IData<Empty>.CreateIterator(QueryIterator iterator) => new Empty(iterator, false);

	static Empty IFilter<Empty>.CreateIterator(QueryIterator iterator) => new Empty(iterator, true);
}
