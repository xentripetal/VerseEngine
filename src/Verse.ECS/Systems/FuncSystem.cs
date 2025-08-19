namespace Verse.ECS.Systems;

/// <summary>
/// 
/// </summary>
/// <param name="set"></param>
/// <param name="OnInit">Optional hook to call when initializing the system. Used for registering custom metadata</param>
public abstract class BaseFuncSystem(string? name = null, ISystemSet? set = null, Action<World, ISystem>? OnInit = null) : ClassSystem(name, set)
{
	public override void Initialize(World world)
	{
		if (OnInit != null)
			OnInit(world, this);
		base.Initialize(world);
	}
}

public partial class FuncSystem(Action fn, string? name = null, ISystemSet? set = null) : BaseFuncSystem(name, set)
{
	public override void Run(World world) => fn();

	public static FuncSystem Of(Action fn, string? name = null, ISystemSet? set = null) => new FuncSystem(fn, name, set);
}