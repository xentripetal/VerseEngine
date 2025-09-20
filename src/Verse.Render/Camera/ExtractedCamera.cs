using Verse.Core;
using Verse.Math;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Camera;
using Verse.Render.Graph;

namespace Verse.Render;

/// <summary>
/// All the necessary data from a camera and its related components to render a frame. Built during render extraction.
/// </summary>
public struct ExtractedCamera
{
	public Texture? Target;
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

public struct CameraOutputMode { }