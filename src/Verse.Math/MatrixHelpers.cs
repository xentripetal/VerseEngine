using System.Numerics;
using System.Runtime.CompilerServices;

namespace Verse.Math;

public static class MatrixHelpers
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Matrix4x4 Inverse(this Matrix4x4 matrix) => Matrix4x4.Invert(matrix, out var inverse) ? inverse : Matrix4x4.Identity;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 ProjectPoint(this Matrix4x4 matrix, Vector3 point)
	{
		var p = new Vector4(point, 1);
		p = Vector4.Transform(p, matrix);
		return (p / new Vector4(p.W, p.W, p.W, p.W)).AsVector3();
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 TransformPoint(this Matrix4x4 matrix, Vector3 point) => Vector3.Transform(point, matrix);
}