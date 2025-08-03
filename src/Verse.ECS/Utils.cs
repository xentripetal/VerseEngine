namespace Verse.ECS;

public static class Extensions
{
	public static void SortNoAlloc<T>(this Span<T> span, Comparison<T> comparison)
	{
#if NET
		span.Sort(comparison);
#else
		span.Sort(comparison);
#endif
	}
}