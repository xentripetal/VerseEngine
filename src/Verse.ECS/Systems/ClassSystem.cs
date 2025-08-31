using Verse.ECS.Scheduling.Configs;

namespace Verse.ECS.Systems;

/// <summary>
/// Base properties for a <see cref="ISystem"/> or <see cref="ICondition"/>.
/// </summary>
public interface IMetaSystem
{
	/// <summary>
	/// Gets the metadata and access information for a system
	/// </summary>
	public SystemMeta Meta { get; }

	/// <summary>
	/// Initialize the system and its parameters.
	/// </summary>
	/// <param name="world"></param>
	public void Initialize(World world);
}

public interface ICondition : IMetaSystem
{
	/// <summary>
	/// Execute the condition and return the result
	/// </summary>
	/// <param name="world"></param>
	public bool Evaluate(World world, uint tick);
}

public interface ISystem : IMetaSystem
{
	/// <summary>
	/// Gets the system set that should be created alongside this system and it should be inserted into.
	/// </summary>
	/// <returns></returns>
	public List<ISystemSet> GetDefaultSystemSets();


	/// <summary>
	/// Execute the system
	/// </summary>
	/// <param name="world"></param>
	public void TryRun(World world, uint tick);

	public void ApplyDeferred(World world);

	public CommandBuffer Buffer { get; }
}

public abstract class ClassSystem : ISystem, IIntoSystemConfigs, IIntoSystemSet
{
	protected ClassSystem(string? name = null, ISystemSet? set = null)
	{
		Meta = new SystemMeta(name ?? GetType().Name);
		Set = set ?? new SystemTypeSet(GetType());
		Params = [];
	}
	protected ISystemSet Set;

	protected ISystemParam[] Params;
	private bool _initialized;
	public void SetParams(params ISystemParam[] parameters)
	{
		if (_initialized)
			throw new InvalidOperationException("Cannot set parameters after the system has been initialized");
		Params = parameters;
	}
	public CommandBuffer Buffer { get; private set; }
	public SystemMeta Meta { get; }
	public virtual void Initialize(World world)
	{
		Buffer = new CommandBuffer(world);
		foreach (var param in Params) {
			param.Init(this, world);
		}
		var attributes = GetCustomAttributes();
		SystemConfigAttribute.ApplyAll(this, attributes);
		_initialized = true;
	}

	public virtual List<ISystemSet> GetDefaultSystemSets()
	{
		return [Set];
	}
	public ISystemSet IntoSystemSet() => Set;

	public virtual void TryRun(World world, uint tick)
	{
		Meta.Ticks.ThisRun = tick;
		foreach (var param in Params) {
			if (!param.Ready(this, world))
				return;
		}
		Run(world);
		Meta.Ticks.LastRun = tick;
	}

	public void ApplyDeferred(World world)
	{
		if (Buffer._operations.Count > 0)
			world.ApplyCommandBuffer(Buffer);
	}

	public abstract void Run(World world);
	public NodeConfigs<ISystem> IntoConfigs()
	{
		IIntoNodeConfigs<ISystem> baseConfig = NodeConfigs<ISystem>.Of(new SystemConfig(this));

		// Apply any attributes of this type onto its base config
		var attributes = GetCustomAttributes();
		return SystemConfigAttribute.ApplyAll(baseConfig, attributes).IntoConfigs();
	}
	
	/// <summary>
	/// Gets attributes for this system. If you are wrapping a method, you should override this to return the attributes of the method.
	/// </summary>
	/// <returns></returns>
	protected virtual Attribute[] GetCustomAttributes()
	{
		return Attribute.GetCustomAttributes(GetType(), true);
	}



	// Re-export all the interface methods from IIntoSystemConfigs to make it easier to chain them

	public IIntoNodeConfigs<ISystem> InSet(IIntoSystemSet set) => IntoConfigs().InSet(set);

	public IIntoNodeConfigs<ISystem> InSet<TEnum>(TEnum set) where TEnum : struct, Enum => IntoConfigs().InSet(set);

	public IIntoNodeConfigs<ISystem> Before(IIntoSystemSet set) => IntoConfigs().Before(set);

	public IIntoNodeConfigs<ISystem> After(IIntoSystemSet set) => IntoConfigs().After(set);

	public IIntoNodeConfigs<ISystem> BeforeIgnoreDeferred(IIntoSystemSet set) => IntoConfigs().BeforeIgnoreDeferred(set);

	public IIntoNodeConfigs<ISystem> AfterIgnoreDeferred(IIntoSystemSet set) => IntoConfigs().AfterIgnoreDeferred(set);

	public IIntoNodeConfigs<ISystem> Chained() => IntoConfigs().Chained();

	public IIntoNodeConfigs<ISystem> ChainedIgnoreDeferred() => IntoConfigs().ChainedIgnoreDeferred();

	public IIntoNodeConfigs<ISystem> RunIf(ICondition condition) => IntoConfigs().RunIf(condition);

	public IIntoNodeConfigs<ISystem> DistributiveRunIf(ICondition condition) => IntoConfigs().DistributiveRunIf(condition);

	public IIntoNodeConfigs<ISystem> AmbiguousWith(IIntoSystemSet set) => IntoConfigs().AmbiguousWith(set);

	public IIntoNodeConfigs<ISystem> AmbiguousWithAll() => IntoConfigs().AmbiguousWithAll();
}