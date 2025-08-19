using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;

namespace ProjectVerse;

public class Example { }

public partial class RunSystems : SystemsContainer
{
	[Schedule]
	[Before<Sets>(Sets.Act2)]
	public void Act1(Query<Data<int>, Changed<int>> b)
	{
		Console.WriteLine("Act1");
	}
	[Schedule]
	[After<Sets>(Sets.Act1)]
	public void Act2(Res<Example> b)
	{
		Console.WriteLine("Act2");
	}
	
	[Schedule]
	[After<Sets>(Sets.Act2)]
	public void Act3(World world)
	{
		Console.WriteLine("Act3");
	}
}