using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Render;

public class Extract<T> : ISystemParam, IFromWorld<Extract<T>>
	where T : ISystemParam, IFromWorld<T> 
{
	public T Param;
	public void Init(ISystem system, World world)
	{
		var mainWorld = world.Resource<MainWorld>();
		Param = T.FromWorld(mainWorld.world);
		Param.Init(system, world);
	}
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun)
	{
		var mainWorld = world.Resource<MainWorld>();
		Param.ValidateParam(meta, mainWorld.world, thisRun);
	}
	public static Extract<T> FromWorld(World world) => new Extract<T>();
}