using Verse.ECS.Scheduling.Configs;

namespace Verse.ECS.Systems;



public abstract class SystemsContainer : IIntoSystemConfigs, IIntoSystemSet
{
	public abstract ISystemSet IntoSystemSet();
	public abstract NodeConfigs<ISystem> IntoConfigs();
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
