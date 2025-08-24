using Microsoft.Extensions.Logging;
using Verse.ECS.Systems;

namespace Verse.ECS.Scheduling.Executor;

public class SingleThreadedExecutor(ILogger<SingleThreadedExecutor> logger) : IExecutor
{
    /// <summary>
    ///     Applies deferred system buffers after all systems have ran
    /// </summary>
    protected bool ApplyFinalDeferred = true;
    /// <summary>
    ///     Systems that have run or been skipped
    /// </summary>
    protected FixedBitSet CompletedSystems;
    /// <summary>
    ///     System sets whose conditions have been evaluated
    /// </summary>
    protected FixedBitSet EvaluatedSets;

    public void Init(SystemSchedule schedule)
    {
        var sysCount = schedule.SystemIds.Count;
        var setCount = schedule.SetIds.Count;
        EvaluatedSets = new FixedBitSet(setCount);
        CompletedSystems = new FixedBitSet(sysCount);
    }

    public void SetApplyFinalDeferred(bool apply)
    {
        ApplyFinalDeferred = apply;
    }

    public void Run(SystemSchedule schedule, World world, FixedBitSet? skipSystems, uint tick)
    {
        if (skipSystems != null)
        {
            CompletedSystems.Or(skipSystems.Value);
        }

        world.BeginDeferred();
        for (var systemIndex = 0; systemIndex < schedule.Systems.Count; systemIndex++)
        {
            var shouldRun = !CompletedSystems.Contains(systemIndex);
            foreach (var setIdx in schedule.SetsWithConditionsOfSystems[systemIndex].Ones())
            {
                if (EvaluatedSets.Contains(setIdx))
                {
                    continue;
                }
                // Evaluate system set's conditions
                var setConditionsMet = EvaluateAndFoldConditions(schedule.SetConditions[setIdx], world);

                // Skip all systems that belong to this set, not just the current one
                if (!setConditionsMet)
                {
                    CompletedSystems.Or(schedule.SystemsInSetsWithConditions[setIdx]);
                }

                shouldRun &= setConditionsMet;
                EvaluatedSets.Set(setIdx);
            }

            // Evaluate System's conditions
            var systemConditionsMet = EvaluateAndFoldConditions(schedule.SystemConditions[systemIndex], world);
            shouldRun &= systemConditionsMet;

            CompletedSystems.Set(systemIndex);
            if (!shouldRun)
            {
                continue;
            }

            var system = schedule.Systems[systemIndex];
            if (system is ApplyDeferredSystem)
            {
                ApplyDeferred(schedule, world);
                continue;
            }

            try
            {
                system.TryRun(world, tick);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error in system {System}", system.Meta.Name);;
            }
        }

        if (ApplyFinalDeferred)
        {
            world.EndDeferred();
        }
        EvaluatedSets.Clear();
        CompletedSystems.Clear();
    }

    protected void ApplyDeferred(SystemSchedule schedule, World world)
    {
        world.EndDeferred();
        world.BeginDeferred();
    }

    protected bool EvaluateAndFoldConditions(List<ICondition> conditions, World world)
    {
        // Not short-circuiting is intentional
        var met = true;
        foreach (var condition in conditions)
        {
            if (!condition.Evaluate(world))
            {
                met = false;
            }
        }
        return met;
    }
}
