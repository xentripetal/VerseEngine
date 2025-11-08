using System.Numerics;
using Verse.Core;
using Verse.Math;
using Verse.MoonWorks;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Camera;
using Verse.Transform;

namespace Verse.Render.View;

/// <summary>
/// Describes a camera in the render world
/// </summary>
/// <remarks>
/// Each entity in the main world can potentially extract to multiple subviews,
/// each of which has a [`RetainedViewEntity::subview_index`]. For instance, 3D
/// cameras extract to both a 3D camera subview with index 0 and a special UI
/// subview with index 1. Likewise, point lights with shadows extract to 6
/// subviews, one for each side of the shadow cubemap.
/// </remarks>
public struct ExtractedView
{
	public RetainedViewEntity RetainedViewEntity;
	public GlobalTransform ClipFromView;
	/// <summary>
	/// The view-projection matrix. When provided it is used instead of deriving it from
	/// `projection` and `transform` fields, which can be helpful in cases where numerical
	/// stability matters and there is a more direct way to derive the view-projection matrix.
	/// </summary>
	public Matrix4x4? ClipFromWorld;
	public bool Hdr;
	/// <summary>
	/// uvec4(origin.x, origin.y, width, height)
	/// </summary>
	public UVec4 Viewport;
	// TODO color grading
}

public struct ViewTargetTexture
{
	public Texture? Texture;
	public Window? Window;
	public Color? ClearColor;
	
	public ViewTargetTexture(Texture texture) => Texture = texture;
	public ViewTargetTexture(Window window) => Window = window;
}
public record struct ViewDepthTexture(Texture Texture);

/// <summary>
/// An identifier for a view that is stable across frames.
/// </summary>
/// <remarks>
/// We can't use <see cref="RenderEntity"/> for this because render world entities aren't
/// stable, and we can't use just <see cref="MainEntity"/> because some main world views
/// extract to multiple render world views. For example, a directional light
/// extracts to one render world view per cascade, and a point light extracts to
/// one render world view per cubemap face. So we pair the main entity with an
/// *auxiliary entity* and a *subview index*, which *together* uniquely identify
/// a view in the render world in a way that's stable from frame to frame.
/// </remarks>
public struct RetainedViewEntity(MainEntity mainEntity, MainEntity? auxEntity, uint subviewIndex)
{
	/// <summary>
	/// The main entity that this view corresponds to.
	/// </summary>
	public MainEntity MainEntity = mainEntity;
	/// <summary>
	/// Another entity associated with the view entity
	/// </summary>
	/// <remarks>
	/// <para>In bevy, this is currently used for shadow cascades. If there are multiple cameras, each camera needs to have its own set of shadow cascades. Thus
	/// the light and subview index aren't themselves enough to uniqely identify a shadow cascde: they need the camera that  the cascade is assocaited with as
	/// well. This entity stores that camera.</para>
	/// <para>If not present, this will be some placeholder entity that doesn't actually exist.</para>
	/// <para>This is currently unused in Verse as I didn't port the 3D logic. But leaving in here for semantics sake.</para>
	/// </remarks>
	public MainEntity AuxEntity = auxEntity ?? new MainEntity(ulong.MaxValue);
	/// <summary>
	/// The index of the view corresponding to the entity
	/// </summary>
	/// <remarks>
	/// For example, for point lights that cast shadows, this is the index of the cubemap face (0 through 5). For directional lights, this is the index
	/// of the cascade.
	/// </remarks>
	public uint SubviewIndex = subviewIndex;
}