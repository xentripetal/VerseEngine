using Verse.ECS;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Graph;
using Verse.Render.Graph.RenderPhase;
using Verse.Render.View;

namespace Verse.Render.Pipeline2D;

public class OpaquePass2DNode : IViewRenderNode<Empty>
{
	public void Run(RenderGraphContext context, RenderContext renderContext, World world, EntityView viewEntity)
	{
		var camera = viewEntity.Get<ExtractedCamera>();
		var view = viewEntity.Get<ExtractedView>();
		var target = viewEntity.Get<ViewTargetTexture>();
		var depth = viewEntity.Get<ViewDepthTexture>();

		if (!world.TryGetResource<ViewBinnedRenderPhases<Opaque2D, Opaque2DBinkey, BatchSetKey2D>>(out var opaquePhases)) {
			return;
		}
		// todo alpha mask
		if (!opaquePhases.TryGetValue(view.RetainedViewEntity, out var opaquePhase)) {
			return;
		}

		renderContext.AddCommandBufferFunc(device => {
			var buffer = device.AcquireCommandBuffer();
			// TODO this doesn't seem right
			Texture? targetTexture;
			if (target.Window != null) {
				targetTexture = buffer.WaitAndAcquireSwapchainTexture(target.Window);
			} else {
				targetTexture = target.Texture;
			}
			if (targetTexture == null) {
				return buffer;
			}

			var renderPass = buffer.BeginRenderPass(
				new ColorTargetInfo(targetTexture, target.ClearColor ?? new Color(0, 0, 0, 0))
			);
			if (camera.Viewport.HasValue) {
				renderPass.SetViewport(camera.Viewport.Value.ToSDL());
			}
			if (!opaquePhase.IsEmpty) {
				opaquePhase.Render(renderPass, world, viewEntity);
			}
			buffer.EndRenderPass(renderPass);
			return buffer;
		});
	}
}