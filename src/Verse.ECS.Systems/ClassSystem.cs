using PolyECS.Scheduling.Configs;
using Verse.ECS;

namespace PolyECS.Systems;

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

	/// <summary>
	/// Called whenever a new table is created. The system should check if the table has any components that it is interested in and update
	/// its metadata accordingly.
	/// </summary>
	/// <param name="cache"></param>
	public void UpdateStorageAccess(ArchetypeRegistry archetypes);
}

public interface ICondition : IMetaSystem
{
	/// <summary>
	/// Execute the condition and return the result
	/// </summary>
	/// <param name="world"></param>
	public bool Evaluate(World world);
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
	public void TryRun(World world);
}

public abstract class ClassSystem : ISystem, IIntoSystemConfigs, IIntoSystemSet
{
	protected ClassSystem(params ISystemParam[] parameters)
	{
		Meta = new SystemMeta(GetType().Name);
		Params = parameters.ToList();
	}



	protected List<ISystemParam> Params;
	public SystemMeta Meta { get; }
	public virtual void Initialize(World world) { }

	protected int TableGeneration;
	protected int ResourceGeneration;

	public void UpdateStorageAccess(ArchetypeRegistry registry)
	{
		throw new Exception("Not implemented");
	}

	public virtual List<ISystemSet> GetDefaultSystemSets()
	{
		return [new SystemTypeSet(GetType())];
	}

	public virtual void TryRun(World world)
	{
		foreach (var param in Params) {
			throw new Exception("Not implemented");
		}
		Run(world);
	}

	public abstract void Run(World world);
	public NodeConfigs<ISystem> IntoConfigs()
	{
		IIntoNodeConfigs<ISystem> baseConfig = NodeConfigs<ISystem>.Of(new SystemConfig(this));

		// Apply any attributes of this type onto its base config
		var attributes = Attribute.GetCustomAttributes(GetType(), true);
		foreach (var attr in attributes) {
			if (attr is SystemConfigAttribute configAttr) {
				baseConfig = configAttr.Apply(baseConfig);
			}
		}

		return baseConfig.IntoConfigs();
	}

	public ISystemSet IntoSystemSet()
	{
		return new SystemTypeSet(GetType());
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