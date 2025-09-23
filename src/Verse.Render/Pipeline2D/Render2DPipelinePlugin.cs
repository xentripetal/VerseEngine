using Verse.Core;
using Verse.ECS;
using Verse.Render.Graph;

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

		var graph = renderApp.World.GetRes<RenderGraph>().Value;
		var graph2D = new RenderGraph();
		graph.AddSubGraph(Render2DGraph.Label(), graph2D);
		graph2D.AddNode(Node2D.StartMainPass.AsRenderLabel(), new EmptyNode());
	}
}