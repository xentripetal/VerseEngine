using System.Numerics;
using System.Runtime.CompilerServices;
using Serilog;
using Verse.Assets;
using Verse.ECS;
using Verse.Math;
using Verse.MoonWorks;
using Verse.MoonWorks.Assets;
using Verse.MoonWorks.Graphics;
using Verse.Transform;
using Rect = Verse.Math.Rect;

namespace Verse.Camera;

public struct Viewport
{
	public Viewport()
	{
		PhysicalPosition = new UVec2();
		PhysicalSize = new UVec2(1, 1);
		Depth = new TRange<float>(0, 1);
	}

	public Verse.MoonWorks.Graphics.Viewport ToSDL()
	{
		return new Verse.MoonWorks.Graphics.Viewport {
			X = PhysicalPosition.X,
			Y = PhysicalPosition.Y,
			W = PhysicalSize.X,
			H = PhysicalSize.Y,
			MinDepth = Depth.Start,
			MaxDepth = Depth.End,
		};
	}

	public Viewport(UVec2 physicalPosition, UVec2 physicalSize, TRange<float> depth)
	{
		PhysicalPosition = physicalPosition;
		PhysicalSize = physicalSize;
		Depth = depth;
	}
	public UVec2 PhysicalPosition;
	/// <summary>
	/// The physical size of the viewport rectangle to render within the <see cref="RenderTarget"/> of this <see cref="Camera"/>
	/// </summary>
	public UVec2 PhysicalSize;
	/// <summary>
	/// The minimum and maximum depth to render (on a scale from 0.0 to 1.0)
	/// </summary>
	public TRange<float> Depth;

	/// <summary>
	/// Cut the viewport rectangle so that it lies inside a rectangle of the
	/// given size.
	/// </summary>
	/// <remarks>
	/// If either of the viewport's position coordinates lies outside the given
	/// dimensions, it will be moved just inside first. If either of the given
	/// dimensions is zero, the position and size of the viewport rectangle will
	/// both be set to zero in that dimension.
	/// </remarks>
	public void ClampToSize(UVec2 size)
	{
		// If the origin of the viewport rect is outside, then adjust so that its barely inside. Then cut off the part that is outside
		if (PhysicalSize.X + PhysicalPosition.X > size.X) {
			if (PhysicalPosition.X < size.X) {
				PhysicalSize.X = size.X - PhysicalPosition.X;
			} else if (size.X > 0) {
				PhysicalPosition.X = size.X - 1;
				PhysicalSize.X = 1;
			} else {
				PhysicalPosition.X = 0;
				PhysicalSize.X = 0;
			}
		}

		if (PhysicalSize.Y + PhysicalPosition.Y > size.Y) {
			if (PhysicalPosition.Y < size.Y) {
				PhysicalSize.Y = size.Y - PhysicalPosition.Y;
			} else if (size.Y > 0) {
				PhysicalPosition.Y = size.Y - 1;
				PhysicalSize.Y = 1;
			} else {
				PhysicalPosition.Y = 0;
				PhysicalSize.Y = 0;
			}
		}
	}
}

/// <summary>
/// Settings to define a camera sub view
/// </summary>
public record struct SubCameraView
{
	/// <summary>
	/// Size of the entire camera view
	/// </summary>
	public UVec2 FullSize;
	/// <summary>
	/// Offset of the sub camera
	/// </summary>
	public UVec2 Offset;
	/// <summary>
	/// Size of the sub camera
	/// </summary>
	public UVec2 Size;
}

public struct Camera 
{
	public Camera()
	{
		IsActive = true;
		Order = 0;
		Viewport = null;
		Computed = new ComputedCameraValues();
		RenderTarget = new RenderTarget();
		OutputMode = new CameraOutputMode();
		MsaaWriteback = true;
		ClearColor = ClearColorConfig.Default();
		SubCameraView = null;
	}

	/// <summary>
	/// If set, this camera will render to the given <see cref="Viewport"/> rectangle with the configured <see cref="RenderTarget"/>
	/// </summary>
	public Viewport? Viewport;
	/// <summary>
	/// Cameras with a higher order are rendered later, and thus on top of lower order cameras
	/// </summary>
	public int Order;
	/// <summary>
	/// If this is set to true, theis camera will be rendered to its specified <see cref="RenderTarget"/>. If false it won't
	/// be rendered.
	/// </summary>
	public bool IsActive;
	/// <summary>
	/// Computed values for this camera, such as the projection matrix and the render target size
	/// </summary>
	public ComputedCameraValues Computed;
	/// <summary>
	/// The target that this camera will render to
	/// </summary>
	public RenderTarget RenderTarget;
	/// <summary>
	/// The <see cref="CameraOutputMode"/> for this camera
	/// </summary>
	public CameraOutputMode OutputMode;
	/// <summary>
	/// If this is enabled, a previous camera exists that shares this camera's render target, and this camera has MSAA enabled, then the previous camera's
	/// outputs will be written to the intermediate multi-sampled render target textures for this camera. This enables cameras with MSAA enabled to
	/// "write their results on top" of previous camera results, and include them as a part of their render results. This is enabled by default to ensure
	/// cameras with MSAA enabled layer their results in the same way as cameras without MSAA enabled by default.
	/// </summary>
	/// <remarks>Might not be implemented, but copying bevy structure for this</remarks>
	public bool MsaaWriteback;
	/// <summary>
	/// The clear color opreation to perform on the render target.
	/// </summary>
	public ClearColorConfig ClearColor;
	/// <summary>
	/// If set, this camera will be a sub camera of a large view, defined by a <see cref="SubCameraView"/>
	/// </summary>
	public SubCameraView? SubCameraView;

	/// <summary>
	/// The full physical size of this camera's <see cref="RenderTarget"/> (in physical pixels), ignoring custom
	/// viewport configuration.
	/// </summary>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UVec2? PhysicalTargetSize() => Computed.RenderTargetInfo?.PhysicalSize;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UVec2? PhysicalViewportSize() => Viewport?.PhysicalSize ?? PhysicalTargetSize();

	/// <summary>
	/// The rendered physical bounds of the camera. If the viewport is set, this will be the rect of that custom viewport.
	/// Otherwise it will default to the full physical rect of the current <see cref="RenderTarget"/>
	/// </summary>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public URect? PhysicalViewportRect()
	{
		var min = Viewport?.PhysicalPosition ?? UVec2.Zero;
		var size = PhysicalViewportSize();
		if (size == null) return null;
		var max = min + size.Value;
		return new URect(min, max);
	}

	/// <summary>
	/// The rendered logical bounds of the camera. If the viewport is set, this will be the rect of that custom viewport.
	/// Otherwise it will default to the full logical rect of the current <see cref="RenderTarget"/>
	/// </summary>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Rect? LogicalViewportRect() => PhysicalViewportRect()?.ToRect();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Vector2? LogicalViewportSize()
	{
		if (Viewport == null) return LogicalTargetSize();
		return ToLogical(Viewport.Value.PhysicalSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Vector2? LogicalTargetSize()
	{
		if (Computed.RenderTargetInfo == null) return null;
		return ToLogical(Computed.RenderTargetInfo.Value.PhysicalSize);
	}

	/// <summary>
	/// Converts a physical size in this Camera to a logical size
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Vector2? ToLogical(UVec2 physicalSize)
	{
		if (Computed.RenderTargetInfo == null) return null;
		var scale = Computed.RenderTargetInfo.Value.ScaleFactor;
		return new Vector2(physicalSize.X / scale, physicalSize.Y / scale);
	}

	/// <summary>
	/// Given a point in world space, use the cameras viewport to compute the Normalized Device Coordinates (NDC) of the point.
	/// </summary>
	/// <remarks>When the point is within the viewport, the values returned will be between -1 and 1 on the x and Y axes and
	/// between 0 and 1 on the z axiz.</remarks>
	public Vector3 WorldToNormalizedDeviceCoordinates(GlobalTransform cameraTransform, Vector3 worldPosition)
	{
		var viewFromWorld = cameraTransform.Inverse;
		var viewPoint = viewFromWorld.TransformPoint(worldPosition);
		return Computed.ClipFromView.ProjectPoint(viewPoint);
	}

	public Vector2? WorldToViewport(GlobalTransform cameraTransform, Vector3 worldPosition)
	{
		var targetRect = PhysicalViewportRect();
		if (targetRect == null) return null;
		var ndcSpaceCoords = WorldToNormalizedDeviceCoordinates(cameraTransform, worldPosition);
		if (ndcSpaceCoords.Z is < 0 or > 1) {
			return null;
		}
		// Flip the y coordinate origin from the bottom to the top (bevy, not sure why)
		ndcSpaceCoords.Y = -ndcSpaceCoords.Y;

		// Once in NDC space, we discard z and map x/y to the viewport
		return (ndcSpaceCoords.AsVector2() + Vector2.One) / (2 * targetRect.Value.Size()) + targetRect.Value.Min;
	}
}

/// <summary>
/// Control how this <see cref="Camera"/> outputs once rendering is completed
/// </summary>
public record struct CameraOutputMode
{
	public CameraOutputMode()
	{
		BlendState = null;
		ClearColor = ClearColorConfig.Default();
	}
	public CameraOutputMode(ColorTargetBlendState blendState, ClearColorConfig clearColor)
	{
		BlendState = blendState;
		ClearColor = clearColor;
	}
	public CameraOutputMode Skip()
	{
		return new CameraOutputMode {
			BlendState = null,
			ClearColor = null
		};
	}
	/// <summary>
	/// The blend state will be used by the pipleine that writes the intermediate render textures to the final render
	/// target texture. If not set, the output will be written as-is, ignoring and the existing data in the final render
	/// target texture.
	/// </summary>
	public ColorTargetBlendState? BlendState;
	/// <summary>
	/// The clear color operation to perform on the final render target texture. If not set, Skip will be used.
	/// </summary>
	public ClearColorConfig? ClearColor;

	/// <summary>
	/// Skip mode will skip writing the cmaera output to the configured render target. The output will remain in the
	/// render targets interemediate textures. A camera with a higher order should write toe the render target using
	/// CameraOutputMode with a BlendState. The Skip mode can easily prevent render results from being displayed, or
	/// cause them to be lost. Only use this if you know what you are doing!
	/// </summary>
	/// <remarks>
	/// In camera setups with multiple active cameras rendering to the same <see cref="RenderTarget"/>, the skip mode
	/// can be used to remove redundant writes to final output texture, removing unnecessary render passes.
	/// </remarks>
	/// <returns></returns>
	public bool IsSkip()
	{
		return BlendState == null && ClearColor == null;
	}
}

public record struct ComputedCameraValues
{
	public Matrix4x4 ClipFromView;
	public RenderTargetInfo? RenderTargetInfo;
	public UVec2? OldViewportSize;
	public SubCameraView? OldSubCameraView;
}

/// <summary>
/// Information about the current <see cref="RenderTarget"/>
/// </summary>
public record struct RenderTargetInfo
{
	public UVec2 PhysicalSize;
	/// <summary>
	/// The sacle factor of this render target
	/// </summary>
	/// <remarks>
	/// When rendering to a window, typically it is a value greater or equal than 1.0,
	/// representing the ratio between the size of the window in physical pixels and the logical size of the window.
	/// </remarks>
	public float ScaleFactor;
}

/// <summary>
/// The target that a <see cref="Camera"/> will render to. 
/// </summary>
/// <remarks>Bevy supports a ManualTextureView here for platforms outside of Bevy. We don't do that.</remarks>
public record struct RenderTarget
{
	public RenderTarget(Handle<Image> handle, float scaleFactor = 1)
	{
		Image = new ImageRenderTarget(handle, scaleFactor);
	}

	public RenderTarget()
	{
		Window = new WindowReference();
	}
	public RenderTarget(UVec2 size)
	{
		Size = size;
	}
	public RenderTarget(WindowReference window)
	{
		Window = window;
	}

	/// <summary>
	/// Window to which the camera's view is rendered.
	/// </summary>
	public WindowReference? Window;
	/// <summary>
	/// Image to which the cmaera's view is rendered
	/// </summary>
	public ImageRenderTarget? Image;
	/// <summary>
	/// The camera won't render to any color target. This is useful when you want a camera that only renders prepasses,
	/// for example a depth prepass.
	/// </summary>
	public UVec2? Size;

	public bool IsWindow => Window != null;
	public bool IsImage => Image != null;
	public bool IsManual => Size != null;

	public readonly NormalizedRenderTarget Normalize(ulong primaryWindow)
	{
		if (IsWindow) {
			var window = Window!.Value.IsPrimaryWindow
				? new NormalizedWindowReference(primaryWindow)
				: new NormalizedWindowReference(Window.Value.EntityId!.Value);
			return new NormalizedRenderTarget(window);
		}
		if (IsManual) {
			return new NormalizedRenderTarget(Size!.Value);
		}
		if (IsImage) {
			return new NormalizedRenderTarget(Image!.Value.Handle, Image.Value.ScaleFactor);
		}
		return new NormalizedRenderTarget();
	}
}

public record struct NormalizedRenderTarget
{
	public NormalizedRenderTarget(Handle<Image> handle, float scaleFactor = 1)
	{
		Image = new ImageRenderTarget(handle, scaleFactor);
	}

	public NormalizedRenderTarget(UVec2 size)
	{
		Size = size;
	}
	public NormalizedRenderTarget(NormalizedWindowReference window)
	{
		Window = window;
	}

	/// <summary>
	/// Window to which the camera's view is rendered.
	/// </summary>
	public NormalizedWindowReference? Window;
	/// <summary>
	/// Image to which the cmaera's view is rendered
	/// </summary>
	public ImageRenderTarget? Image;
	/// <summary>
	/// The camera won't render to any color target. This is useful when you want a camera that only renders prepasses,
	/// for example a depth prepass.
	/// </summary>
	public UVec2? Size;

	public bool IsWindow => Window != null;
	public bool IsImage => Image != null;
	public bool IsManual => Size != null;
	public bool IsChanged(HashSet<ulong> changedWindowIds, HashSet<AssetId<Image>> changedImages)
	{
		if (Window != null) {
			return changedWindowIds.Contains(Window.Value.EntityId);
		}
		if (Image != null) {
			return changedImages.Contains(Image.Value.Handle.Id());
		}
		return false;
	}
	public RenderTargetInfo GetRenderTargetInfo(Query<Data<Window>> windowEntities, Assets<Image> images)
	{
		if (Window != null) {
			foreach (var (entity, window) in windowEntities) {
				if (entity.Ref.Id == Window.Value.EntityId) {
					return new RenderTargetInfo {
						PhysicalSize = new UVec2(window.Ref.Width, window.Ref.Height),
						ScaleFactor = window.Ref.DisplayScale
					};
				}
			}
			throw new Exception("Window entity not found for render target");
		}
		if (Image != null) {
			if (images.TryGet(Image.Value.Handle, out var image)) {
				return new RenderTargetInfo {
					PhysicalSize = new UVec2(image.Width, image.Height),
					ScaleFactor = Image.Value.ScaleFactor
				};
			}
			throw new Exception("Image not found for render target");
		}
		return new RenderTargetInfo {
			PhysicalSize = Size!.Value,
			ScaleFactor = 1.0f
		};
	}
}

/// <summary>
/// A render target that renders to an <see cref="Image"/>
/// </summary>
/// <param name="Handle"></param>
/// <param name="ScaleFactor"></param>
public record struct ImageRenderTarget(Handle<Image> Handle, float ScaleFactor);

/// <summary>
/// A reference to a window. If the EntityId is null, it refers to the primary window. Otherwise it refers to an entity
/// containing the window
/// </summary>
/// <param name="EntityId"></param>
public record struct WindowReference(ulong? EntityId)
{
	public bool IsPrimaryWindow => EntityId == null;
}

/// <summary>
/// A reference to an entity with a window.
/// </summary>
/// <param name="EntityId"></param>
public record struct NormalizedWindowReference(ulong EntityId);