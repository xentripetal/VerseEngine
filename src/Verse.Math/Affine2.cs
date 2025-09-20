using System.Numerics;

namespace Verse.Math;

/// <summary>
///     A 2D affine transform, which can represent translation, rotation, scaling and shear.
/// </summary>
public struct Affine2
{
	public Affine2(Vector2 translation, Matrix2x2 matrix2)
	{
		Matrix2 = matrix2;
		Translation = translation;
	}

	public Affine2(Vector2 translation, float radians, Vector2 scale)
	{
		Matrix2 = new Matrix2x2(scale, radians);
		Translation = translation;
	}

	public Affine2(Vector2 translation, float radians)
	{
		Matrix2 = new Matrix2x2(Vector2.One, radians);
		Translation = translation;
	}

	public Affine2(Vector2 translation)
	{
		Matrix2 = Matrix2x2.Identity;
		Translation = translation;
	}

	public Affine2()
	{
		Matrix2 = Matrix2x2.Identity;
		Translation = Vector2.Zero;
	}


	public Matrix2x2 Matrix2;
	public Vector2 Translation;

	/// <summary>
	///     Returns the identity matrix.
	/// </summary>
	public static Affine2 Identity { get; } = new (Vector2.Zero, Matrix2x2.Identity);

	public static Affine2 Zero { get; } = new (Vector2.Zero, Matrix2x2.Zero);

	public float RotationDegrees {
		get => Matrix2.RotationDegrees;
		set => Matrix2.RotationDegrees = value;
	}

	public float RotationRadians {
		get => Matrix2.Rotation;
		set => Matrix2.Rotation = value;
	}

	public Vector2 Scale {
		get => Matrix2.Scale;
		set => Matrix2.Scale = value;
	}

	public static void Multiply(in Affine2 a, in Affine2 b, out Affine2 res)
	{
		Matrix2x2.Multiply(a.Matrix2, b.Translation, out res.Translation);
		res.Translation += a.Translation;
		Matrix2x2.Multiply(a.Matrix2, b.Matrix2, out res.Matrix2);
	}

	public static Affine2 operator *(in Affine2 a, in Affine2 b)
	{
		Affine2 res;
		Multiply(a, b, out res);
		return res;
	}
}