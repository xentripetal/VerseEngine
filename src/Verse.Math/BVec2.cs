using System.Runtime.CompilerServices;

namespace Verse.Math;

/// <summary>
/// A 2D boolean vector for component-wise comparisons
/// </summary>
public struct BVec2
{
	public bool X, Y;

	public BVec2(bool x, bool y)
	{
		X = x;
		Y = y;
	}

	/// <summary>
	/// Returns true if all components are true
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool All() => X && Y;

	/// <summary>
	/// Returns true if any component is true
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Any() => X || Y;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static BVec2 operator &(BVec2 a, BVec2 b) => new(a.X && b.X, a.Y && b.Y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static BVec2 operator |(BVec2 a, BVec2 b) => new(a.X || b.X, a.Y || b.Y);
}