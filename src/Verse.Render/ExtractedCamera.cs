using Verse.Camera;
using Verse.Core;
using Verse.Math;
using Verse.Render.Graph;
using Verse.Render.View;

namespace Verse.Render;

public struct CameraPlugin : IPlugin
{

	public void Build(App app)
	{
		app.RegisterRequiredComponents<Camera.Camera, Msaa>();
		app.RegisterRequiredComponents<Camera.Camera, SyncToRenderWorld>();
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