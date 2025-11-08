using Verse.Core;
using Verse.ECS;
using Verse.Render.Graph;
using Verse.Render.Graph.RenderPhase;
using Verse.Render.View;

namespace Verse.Render.Pipeline2D;

/// <summary>
/// <see cref="ISubgraphLabel"/> for the 2d render pipeline. Uses nodes in <see cref="Node2D"/>
/// </summary>
[Label<ISubgraphLabel>]
public partial struct Render2DGraph { }

public enum Node2D
{
	MsaaWriteback,
	StartMainPass,
	MainOpaquePass,
	MainTransparentPass,
	EndMainPass,
	Wireframe,
	Bloom,
	PostProcessing,
	Tonemapping,
	Fxaa,
	Smaa,
	Upscaling,
	ContrastAdaptiveSharpening,
	EndMainPassPostProcessing,
}

public class Render2DPipelinePlugin : IPlugin
{
	public void Build(App app)
	{
		// todo camera

		var renderApp = app.GetSubApp(RenderApp.Name);
		if (renderApp == null) {
			return;
		}

		renderApp.InitResource<DrawFunctions<Opaque2D>>().
			InitResource<ViewBinnedRenderPhases<Opaque2D, Opaque2DBinkey, BatchSetKey2D>>();

		var graph = renderApp.World.Resource<RenderGraph>();
		var graph2D = new RenderGraph();
		graph.AddSubGraph(Render2DGraph.Label(), graph2D);
		graph2D.AddNode(Node2D.StartMainPass.AsRenderLabel(), new EmptyNode());
		graph2D.AddNode(Node2D.EndMainPass.AsRenderLabel(), new EmptyNode());
	}

	public static void ExtractCore2DCameraPhases(
		ResMut<ViewBinnedRenderPhases<Opaque2D, Opaque2DBinkey, BatchSetKey2D>> opaque2DPhases, Extract<Query<Data<Camera>>, Local<HashSet<RetainedViewEntity>> liveEntities)
	{
		liveEntities.Value.Clear();
		foreach (var VARIABLE in ca) {
			
		}
		
	}
}