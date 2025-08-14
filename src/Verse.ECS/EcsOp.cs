namespace Verse.ECS;

public static class IDOp
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID GetGeneration(EcsID id) => (id & EcsConst.ECS_GENERATION_MASK) >> 32;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID IncreaseGeneration(EcsID id) => id & ~EcsConst.ECS_GENERATION_MASK | (0xFFFF & GetGeneration(id) + 1) << 32;
}