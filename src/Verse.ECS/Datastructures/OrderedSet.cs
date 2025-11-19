namespace Verse.ECS.Datastructures;

public interface IReadOnlyOrderedSet<T> : IReadOnlySet<T>
{
	T this[int index] { get; }

	int IndexOf(T item);

	public bool TryGetIndexOf(T Item, out int index);
}