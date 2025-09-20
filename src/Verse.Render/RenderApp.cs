using Verse.Core;
using Verse.ECS;
using Verse.ECS.Scheduling;
using Verse.Render.Graph;

namespace Verse.Render;

public class RenderApp : SubApp
{
	public const string Name = "Render";
	public RenderApp() : base(Name, RenderSchedules.Render, RenderSchedules.Render, ExecutorKind.SingleThreaded) { }

}