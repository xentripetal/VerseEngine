using System.Numerics;

namespace Verse.Transform;

public record struct GlobalTransform(Matrix4x4 Value)
{
	public static implicit operator Matrix4x4(GlobalTransform transform) => transform.Value;
	public static implicit operator GlobalTransform(Matrix4x4 transform) => new GlobalTransform(transform);
	
	public Matrix4x4 Inverse => Matrix4x4.Invert(Value, out var result) ? result : Matrix4x4.Identity;

	// todo add helpers
}

/// <summary>
/// Describe the position of an entity. If the entity has a parent, the position is relative to its parent position.
/// </summary>
public struct Transform
{
	/// <summary>
	/// The position of the entity. In 2D, the last value of the vector is used for z-ordering.
	/// </summary>
	public Vector3 Translation;
	/// <summary>
	/// The rotation of the entity.
	/// </summary>
	public Quaternion Rotation;
	/// <summary>
	/// The scale of the entity.
	/// </summary>
	public Vector3 Scale;
}