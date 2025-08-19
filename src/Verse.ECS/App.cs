using Verse.ECS.Scheduling.Configs;

namespace Verse.ECS;

public class App
{
	public App()
	{
		
	}
	
	public readonly World DefaultWorld;
	public readonly World[] Worlds;
	public App AddSystems(IIntoSystemConfigs node, string schedule = "default")
	{
		return this;
	}

}