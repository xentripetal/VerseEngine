using Verse.Core;
using Verse.ECS.Scheduling;

namespace Verse.Render;

public class RenderApp : SubApp
{
	public RenderApp() : base("Render", RenderSchedules.Render, RenderSchedules.Render, ExecutorKind.SingleThreaded)
	{
		
	}
}