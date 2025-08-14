// See https://aka.ms/new-console-template for more information

using ProjectVerse;
using Verse.ECS;
using Verse.MoonWorks;
using Verse.MoonWorks.Graphics;

var world = new World();

world.Archetypes.OnArchetypeCreated += (world1, archetype) => {
	Console.WriteLine($"arch created {archetype.Generation} - {archetype.HashId}");
};

var entityA = world.Entity().Set(1);//.Set(new List<int>());
var entityb = world.Entity().Set(true).Set(2);

var scheduler = new Scheduler(world);


var a = scheduler.AddSystem((
	Commands commands,
	Query<Data<int>> q
) => {
	foreach (var (a, b) in q) {
		Console.WriteLine($"Data: {a.Ref.ID}, {b.Ref}");
	}
});

scheduler.RunOnce();

