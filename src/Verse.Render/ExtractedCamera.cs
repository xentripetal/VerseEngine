using Verse.Assets;
using Verse.Camera;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;
using Verse.Math;
using Verse.MoonWorks;
using Verse.MoonWorks.Assets;
using Verse.Render.Graph;
using Verse.Render.View;

namespace Verse.Render;

public class CameraUpdateSet : StaticSystemSet
{
	public static CameraUpdateSet Set => new CameraUpdateSet();
}

public class SortedCameras
{
	public List<SortedCamera> Cameras = new List<SortedCamera>();
}

public record struct SortedCamera
{
	public ulong Entity;
	public int Order;
	public NormalizedRenderTarget? Target;
	public bool HDR;
}

public struct CameraPlugin : IPlugin
{
	public void Build(App app)
	{
		var cameraSystem = CameraSystem;
		app.RegisterRequiredComponents<Camera.Camera, Msaa>().
			RegisterRequiredComponents<Camera.Camera, SyncToRenderWorld>().
			// todo extract CameraMainTextureUsages
			AddPlugin<ExtractResourcePlugin<ClearColor>>().
			AddSystems(Schedules.PostStartup, FuncSystem.Of(cameraSystem).InSet(CameraUpdateSet.Set)).
			AddSystems(Schedules.PostUpdate, FuncSystem.Of(cameraSystem).InSet(CameraUpdateSet.Set).
				Before(AssetSystems.AssetEventSystems));
		// TODO before visibility update_frusta


		var renderApp = app.GetSubApp(RenderApp.Name);
		if (renderApp != null) {
			var extractCameras = ExtractCameras;
			var sortCameras = SortCameras;
			renderApp.InitResource<SortedCameras>().
				AddSystems(RenderSchedules.Extract, FuncSystem.Of(extractCameras)).
				AddSystems(RenderSchedules.Render, FuncSystem.Of(sortCameras).InSet(RenderSets.ManageViews));
			
		}
	}

	// todo support other projections
	public static void CameraSystem(
		MessageReader<AssetEvent<Image>> imageEvents,
		Single<Empty, With<PrimaryWindow>> primaryWindow, Query<Data<Window>> windows, Res<Assets<Image>> images,
		Query<Data<Camera.Camera, OrthographicProjection>> cameras)
	{
		var primaryWindowEntity = primaryWindow.GetEntity();
		// TODO populate from window events
		HashSet<ulong> changedWindowIds = new HashSet<ulong>();
		HashSet<ulong> scaleFactorChangedWindowIds = new HashSet<ulong>();

		HashSet<AssetId<Image>> changedImages = new HashSet<AssetId<Image>>();
		foreach (var evt in imageEvents) {
			switch (evt.Type) {
				case AssetEventType.Modified:
				case AssetEventType.Added:
					changedImages.Add(evt.Id);
					break;
			}
		}

		foreach (var (camera, projection) in cameras) {
			ref var camMut = ref camera.Mut;
			var viewportSize = camMut.Viewport?.PhysicalSize;
			var normalizedTarget = camMut.RenderTarget.Normalize(primaryWindowEntity);
			if (normalizedTarget.IsChanged(changedWindowIds, changedImages)
			    || camera.IsAdded()
			    || projection.IsChanged()
			    || camMut.Computed.OldViewportSize != viewportSize
			    || camMut.Computed.OldSubCameraView != camMut.SubCameraView) {

				var newComputedTargetInfo = normalizedTarget.GetRenderTargetInfo(windows, images);
				// Check for the scale factor changing, and resize the viewport if needed.
				// This can happen when the window is moved between monitors with different DPIs.
				// Without this, the viewport will take a smaller portion of the window moved to
				// a higher DPI monitor.
				if (camMut.Computed.RenderTargetInfo.HasValue &&
				    camMut.Viewport.HasValue &&
				    normalizedTarget.IsChanged(scaleFactorChangedWindowIds, new HashSet<AssetId<Image>>(0))) {
					var resizeFactor = newComputedTargetInfo.ScaleFactor / camMut.Computed.RenderTargetInfo.Value.ScaleFactor;
					camMut.Viewport = camMut.Viewport!.Value with {
						PhysicalPosition = camMut.Viewport.Value.PhysicalPosition * resizeFactor,
						PhysicalSize = camMut.Viewport.Value.PhysicalSize * resizeFactor
					};
					viewportSize = camMut.Viewport.Value.PhysicalSize;
				}

				if (camMut.Viewport.HasValue) {
					camMut.Viewport.Value.ClampToSize(newComputedTargetInfo.PhysicalSize);
				}

				camMut.Computed.RenderTargetInfo = newComputedTargetInfo;

				var logicalSize = camMut.LogicalViewportSize();
				if (logicalSize.HasValue && logicalSize.Value.X != 0 && logicalSize.Value.Y != 0) {
					projection.Mut.Update(logicalSize.Value.X, logicalSize.Value.Y);
					camMut.Computed.ClipFromView = (camMut.SubCameraView.HasValue)
						? projection.Ref.GetClipFromViewForSub(camMut.SubCameraView.Value)
						: projection.Ref.GetClipFromView();
				}
			}

			if (camMut.Computed.OldViewportSize == null || camMut.Computed.OldViewportSize != viewportSize) {
				camMut.Computed.OldViewportSize = viewportSize;
			}
			if (camMut.Computed.OldViewportSize == null || camMut.Computed.OldSubCameraView != camMut.SubCameraView) {
				camMut.Computed.OldSubCameraView = camMut.SubCameraView;
			}
		}
	}


	public static void ExtractCameras()
	{
		
	}

	public static void SortCameras()
	{
		
	}

}

/// <summary>
/// All the necessary data from a camera and its related components to render a frame. Built during render extraction.
/// </summary>
public struct ExtractedCamera
{
	public NormalizedRenderTarget? Target;
	public UVec2? PhysicalViewportSize;
	public UVec2? PhysicalTargetSize;
	public Viewport? Viewport;
	public ISubgraphLabel RenderGraph;
	public int Order;
	public CameraOutputMode OutputMode;
	public bool MsaaWriteback;
	public ClearColorConfig ClearColor;
	public int SortedCameraIndexForTarget;
	public float Exposure;
	public bool HDR;
}