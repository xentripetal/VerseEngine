namespace Verse.ECS.Scheduling.Executor;

public interface IExecutor
{
    void Init(SystemSchedule schedule);
    void SetApplyFinalDeferred(bool apply);
    void Run(SystemSchedule executable, World world, FixedBitSet? skipSystems, uint tick);
}
