using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Systems;

namespace Verse.Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
public partial class SystemClassBenchmarks
{
	private World _world;
	private Schedule _schedule;
	private EntityView _entity;
	[ParamsSource(nameof(SystemTypeValues))]
	public string SystemType { get; set; }

	public IEnumerable<string> SystemTypeValues => new[] { "FuncSystem", "ClassSystem", "FuncSystemStatic", "ClassSystemStatic"};

	[GlobalSetup]
	public void Setup()
	{
		_world = new World();
		_world.SetRes(1);
		_world.SetRes<long>(2);
		_entity = _world.Entity().Set(1);
		_schedule = new Schedule("name", ExecutorKind.SingleThreaded);

		switch (SystemType)
		{
			case "FuncSystem":
				_schedule.AddSystems(new FuncSystem<Res<int>, ResMut<long>, Query<Data<int>>>(DoSystemLogic));
				break;
			case "ClassSystem":
				_schedule.AddSystems(new DoSystemLogicSystem(this));
				break;
			case "FuncSystemStatic":
				_schedule.AddSystems(new FuncSystem<Res<int>, ResMut<long>, Query<Data<int>>>(DoSystemLogicStatic));
				break;
			case "ClassSystemStatic":
				_schedule.AddSystems(new DoSystemLogicStaticSystem());
				break;
		}
		_schedule.Run(_world);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_world.Dispose();
		
	}

	[Schedule]
	private void DoSystemLogic(Res<int> r1, ResMut<long> r2, Query<Data<int>> q)
	{
		foreach (var (_, i) in q) {
			i.Mut += r1.Value + (int)r2.Value;
		}
	}
	
	[Schedule]
	private static void DoSystemLogicStatic(Res<int> r1, ResMut<long> r2, Query<Data<int>> q)
	{
		foreach (var (_, i) in q) {
			i.Mut += r1.Value + (int)r2.Value;
		}
	}

	[Benchmark]
	public void RunSystem()
	{
		_schedule.Run(_world);
	}
}