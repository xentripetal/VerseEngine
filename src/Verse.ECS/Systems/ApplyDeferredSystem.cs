namespace Verse.ECS.Systems;

/// <summary>
///     A special marker system that applies deferred buffers.
/// </summary>
public sealed class ApplyDeferredSystem : ClassSystem
{
	public override void Initialize(World world)
	{
		Meta.Access.CombinedAccess.SetWritesAll();
		Meta.HasDeferred = false;
		Meta.IsExclusive = true;
	}

	public override List<ISystemSet> GetDefaultSystemSets() => [new SystemReferenceSet(this)];

	public override void Run(World world) { }
}