using Verse.Math;

namespace Verse.Camera;

public struct Viewport
{
	public Viewport()
	{
		PhysicalPosition = new UVec2();
		PhysicalSize = new UVec2(1, 1);
		Depth = new TRange<float>(0, 1);
	}

	public Viewport(UVec2 physicalPosition, UVec2 physicalSize, TRange<float> depth)
	{
		PhysicalPosition = physicalPosition;
		PhysicalSize = physicalSize;
		Depth = depth;
	}
	public UVec2 PhysicalPosition;
	public UVec2 PhysicalSize;
	public TRange<float> Depth;
}

public struct Camera
{
	/// <summary>
	/// If set, this camera will render to the given <see cref="Viewport"/> rectangle with the configured <see cref="RenderTarget"/>
	/// </summary>
	Viewport? Viewport;
	/// <summary>
	/// Cameras with a higher order are rendered later, and thus on top of lower order cameras
	/// </summary>
	public int Order;
	/// <summary>
	/// If this is set to true, theis camera will be rendered to its specified <see cref="RenderTarget"/>. If false it won't
	/// be rendered.
	/// </summary>
	public bool IsActive;

}

public struct RenderTarget
{
	
	
}