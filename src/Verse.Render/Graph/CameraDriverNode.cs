using System.Runtime.CompilerServices;
using Verse.ECS;
using Verse.MoonWorks;
using Verse.Render.View.Windows;

namespace Verse.Render.Graph;

public class CameraDriverNode : IRenderNode
{

	public void Run(RenderGraphContext context, RenderContext renderContext, World world)
	{
		var sortedCameras = world.Resource<SortedCameras>();
		var windows = world.Resource<ExtractedWindows>();
		HashSet<ulong> cameraWindows = new HashSet<ulong>();

		foreach (var cam in sortedCameras.Cameras) {
			ref var camera = ref world.Entity(cam.Entity).Get<ExtractedCamera>();
			if (Unsafe.IsNullRef(ref camera)) {
				continue;
			}

			var runGraph = true;
			if (camera.Target != null && camera.Target.Value.IsWindow) {
				var targetEntity = camera.Target.Value.Window!.Value;
				if (windows.Windows.TryGetValue(targetEntity.EntityId, out var targetWindow))
				{
					if (targetWindow.Window.Width > 0 && targetWindow.Window.Height > 0) {
						cameraWindows.Add(targetEntity.EntityId);
					} else {
						runGraph = false;
					}
				} else {
					runGraph = false;
				}
			}

			if (runGraph) {
				context.RunSubGraph(camera.RenderGraph, new List<SlotValue>(), cam.Entity);
			}
		}
		
		// bevy will check for any cameras that didn't actually render and do a clear frame to prevent crashes.
		// I don't think thats an issue in SDL3 so skipping it
	}
}