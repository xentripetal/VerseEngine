// See https://aka.ms/new-console-template for more information

using Verse.ECS;
Console.WriteLine("Hello, World!");

var world = new World();
var e = world.Entity("name");
world.Set(e, 123);