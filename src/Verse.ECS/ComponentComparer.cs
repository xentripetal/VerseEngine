namespace Verse.ECS;

internal sealed class ComponentComparer :
	IComparer<ulong>,
	//IComparer<Term>,
	IComparer<ComponentInfo>
{
	private readonly World _world;

	public ComponentComparer(World world)
	{
		_world = world;
	}


	public int Compare(ComponentInfo x, ComponentInfo y) => CompareTerms(_world, x.ID, y.ID);

	public int Compare(ulong x, ulong y) => CompareTerms(_world, x, y);

	// public int Compare(Term x, Term y)
	// {
	// 	return CompareTerms(_world, x.ID, y.ID);
	// }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CompareTerms(World world, ulong a, ulong b) => a.CompareTo(b);
}