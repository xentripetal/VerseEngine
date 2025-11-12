using Verse.Camera;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;
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

		var extract2DCameraPhases = ExtractCore2DCameraPhases;
		renderApp.InitResource<DrawFunctions<Opaque2D>>().
			InitResource<ViewBinnedRenderPhases<Opaque2D, Opaque2DBinkey, BatchSetKey2D>>().
			AddSystems(RenderSchedules.Extract, FuncSystem.Of(extract2DCameraPhases));
		// todo transparents, alphamask, and depth textures

		var graph = renderApp.World.Resource<RenderGraph>();
		var graph2D = new RenderGraph();
		graph.AddSubGraph(Render2DGraph.Label(), graph2D);
		graph2D.AddNode(Node2D.StartMainPass.AsRenderLabel(), new EmptyNode());
		graph2D.AddNode(Node2D.EndMainPass.AsRenderLabel(), new EmptyNode());
	}

	public static void ExtractCore2DCameraPhases(
		ResMut<ViewBinnedRenderPhases<Opaque2D, Opaque2DBinkey, BatchSetKey2D>> opaque2DPhases, Extract<Query<Data<Camera.Camera>, With<Camera2D>>> cameras,
		Local<HashSet<RetainedViewEntity>> liveEntities)
	{
		liveEntities.Value.Clear();
		foreach (var (entity, cam) in cameras.Param) {
			if (!cam.Ref.IsActive) continue;
			var retainedViewEntity = new RetainedViewEntity(new MainEntity(entity.Ref.Id), null, 0);
			// TODO opaque and alpha mask
			opaque2DPhases.Value.PrepareForNewFrame(retainedViewEntity);
			liveEntities.Value.Add(retainedViewEntity);
		}
		
		// clear out all dead views
		var toRemove = new List<RetainedViewEntity>();
		foreach (var entry in opaque2DPhases.Value.Keys) {
			if (!liveEntities.Value.Contains(entry)) {
				toRemove.Add(entry);
			}
		}
		foreach (var entry in toRemove) {
			opaque2DPhases.Value.Remove(entry);
		}
	}
}