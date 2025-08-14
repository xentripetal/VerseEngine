namespace Verse.ECS;

public static class EcsIdEx
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsValid(this EcsID id)
		=> id != 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Generation(this EcsID id)
		=> (int)IDOp.GetGeneration(id);
}