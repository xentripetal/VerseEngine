using System.Numerics;
using System.Runtime.CompilerServices;

namespace Verse.Math;

public record struct UVec2 : IComparable<UVec2>
{
	public uint X, Y;

	public UVec2(uint x, uint y)
	{
		X = x;
		Y = y;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 Splat(uint value) => new UVec2(value, value);
	
	public static readonly UVec2 Min = Splat(uint.MinValue);
	public static readonly UVec2 Max = Splat(uint.MaxValue);

	public static UVec2 Zero => new (0, 0);
	public static UVec2 One => new (1, 1);
	public static UVec2 UnitX => new (1, 0);
	public static UVec2 UnitY => new (0, 1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator +(UVec2 a, UVec2 b) => new (a.X + b.X, a.Y + b.Y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator -(UVec2 a, UVec2 b) => new (a.X - b.X, a.Y - b.Y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator *(UVec2 a, UVec2 b) => new (a.X * b.X, a.Y * b.Y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator /(UVec2 a, UVec2 b) => new (a.X / b.X, a.Y / b.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator +(UVec2 a, uint b) => new (a.X + b, a.Y + b);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator -(UVec2 a, uint b) => new (a.X - b, a.Y - b);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator *(UVec2 a, uint b) => new (a.X * b, a.Y * b);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator /(UVec2 a, uint b) => new (a.X / b, a.Y / b);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UVec2 operator *(uint b, UVec2 a) => new (a.X * b, a.Y * b);
	
	public static UVec2 operator *(UVec2 a, float b) => new ((uint)(a.X * b), (uint)(a.Y * b));
	public static UVec2 operator *(float a, UVec2 b) => new ((uint)(a * b.X), (uint)(a * b.Y));
	
	public static UVec2 operator /(UVec2 a, float b) => new ((uint)(a.X / b), (uint)(a.Y / b));
	public static UVec2 operator /(float a, UVec2 b) => new ((uint)(a / b.X), (uint)(a / b.Y));

	/// <summary>
	/// Returns a vector with the minimum components of this and another vector
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UVec2 MinComponents(UVec2 other) => new(System.Math.Min(X, other.X), System.Math.Min(Y, other.Y));

	/// <summary>
	/// Returns a vector with the maximum components of this and another vector
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UVec2 MaxComponents(UVec2 other) => new(System.Math.Max(X, other.X), System.Math.Max(Y, other.Y));

	/// <summary>
	/// Returns a vector mask where each component is true if this component >= other component
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BVec2 CompareGreaterOrEqual(UVec2 other) => new(X >= other.X, Y >= other.Y);

	/// <summary>
	/// Returns a vector mask where each component is true if this component <= other component
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BVec2 CompareLessOrEqual(UVec2 other) => new(X <= other.X, Y <= other.Y);

	/// <summary>
	/// Add a signed value to unsigned components with saturation (clamps at uint.MinValue and uint.MaxValue)
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UVec2 SaturatingAddSigned(int value)
	{
		uint newX = value >= 0
			? (X > uint.MaxValue - (uint)value ? uint.MaxValue : X + (uint)value)
			: (X < (uint)(-value) ? uint.MinValue : X - (uint)(-value));
		uint newY = value >= 0
			? (Y > uint.MaxValue - (uint)value ? uint.MaxValue : Y + (uint)value)
			: (Y < (uint)(-value) ? uint.MinValue : Y - (uint)(-value));
		return new UVec2(newX, newY);
	}
	
	public Vector2 AsVector2() => new(X, Y);
	public static implicit operator Vector2(UVec2 v) => v.AsVector2();
	public int CompareTo(UVec2 other)
	{
		var xComparison = X.CompareTo(other.X);
		if (xComparison != 0) return xComparison;
		return Y.CompareTo(other.Y);
	}
	public bool Equals(UVec2 other) => X == other.X && Y == other.Y;
	public override int GetHashCode() => HashCode.Combine(X, Y);
}