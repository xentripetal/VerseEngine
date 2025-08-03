namespace Verse.ECS;

public static class IDOp
{
	public static void Toggle(ref EcsID id)
	{
		id ^= EcsConst.ECS_TOGGLE;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID GetGeneration(EcsID id) => (id & EcsConst.ECS_GENERATION_MASK) >> 32;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID IncreaseGeneration(EcsID id) => id & ~EcsConst.ECS_GENERATION_MASK | (0xFFFF & GetGeneration(id) + 1) << 32;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID RealID(EcsID id)
	{
		return id &= EcsConst.ECS_ENTITY_MASK;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasFlag(EcsID id, byte flag) => (id & flag) != 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsComponent(EcsID id) => (id & ~EcsConst.ECS_COMPONENT_MASK) != 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static EcsID SetAsComponent(EcsID id)
	{
		return id |= EcsConst.ECS_ID_FLAGS_MASK;
	}
}