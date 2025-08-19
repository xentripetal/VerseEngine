// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Logging;
using ProjectVerse;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.ECS.Scheduling.Executor;

var app = new App();
var world = new World();


var factory = LoggerFactory.Create(builder => {
	builder.AddConsole();
});

var entityA = world.Entity().Set((int) 1);//.Set(new List<int>());
var entityb = world.Entity().Set(true).Set(2);
var schedules = new Schedule("main", new SimpleExecutor(new Logger<SimpleExecutor>(factory)));
var dummy = new RunSystems();
schedules.AddSystems(dummy);
schedules.Run(world);
schedules.Run(world);
