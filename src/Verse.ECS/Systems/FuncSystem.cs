namespace Verse.ECS.Systems;

/// <summary>
/// 
/// </summary>
public abstract class BaseFuncSystem : ClassSystem
{
	private Action<World, ISystem>? OnInit;
	/// <summary>
	/// 
	/// </summary>
	/// <param name="set">Override default set for this system. If not set it will use a <see cref="SystemReferenceSet"/></param>
	/// <param name="attributes">Optional attributes for the source of this func. Allows any <see cref="Verse.ECS.SystemConfigAttribute"/>s or <see cref="Verse.ECS.SystemInitAttribute"/>s to be passed through.</param>
	protected BaseFuncSystem(string? name = null, ISystemSet? set = null, Attribute[]? attributes = null) : base(name, set)
	{
		if (set == null) {
			Set = new SystemReferenceSet(this);
		}
		Attributes = attributes ?? [];
	}

	protected Attribute[] Attributes;

	protected override Attribute[] GetCustomAttributes() => Attributes;

	public override void Initialize(World world)
	{
		base.Initialize(world);
		if (OnInit != null)
			OnInit(world, this);
	}
}

public partial class FuncSystem(Action fn, string? name = null, ISystemSet? set = null, Attribute[] attributes = null) : BaseFuncSystem(name, set, attributes)
{
	public override void Run(World world) => fn();

	public static FuncSystem Of(Action fn, string? name = null, ISystemSet? set = null, Attribute[] attributes = null) => new FuncSystem(fn, name, set, attributes);
}