using System.Numerics;
using System.Runtime.CompilerServices;

namespace Verse.Math;

public record struct Rect
{
	/// <summary>
	/// Create a new rectangle from two corner points.
	/// </summary>
	/// <remarks>
	/// The two points do not need to be the minimum and/or maximum corners.
	/// They only need to be two opposite corners.
	/// </remarks>
	/// <example>
	/// <code>
	/// // Unit rect from [0,0] to [1,1]
	/// var r = Rect.FromCorners(Vector2.Zero, Vector2.One); // w=1 h=1
	/// // Same; the points do not need to be ordered
	/// var r = Rect.FromCorners(Vector2.One, Vector2.Zero); // w=1 h=1
	/// </code>
	/// </example>
	public Rect(Vector2 min, Vector2 max)
	{
		Min = min;
		Max = max;
	}
	/// <summary>
	/// Create a new rectangle from two corner points.
	/// </summary>
	/// <remarks>
	/// The two points do not need to be the minimum and/or maximum corners.
	/// They only need to be two opposite corners.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 4f, 10f, 6f); // w=10 h=2
	/// var r = Rect.New(2f, 3f, 5f, -1f); // w=3 h=4
	/// </code>
	/// </example>
	public Rect(float minX, float minY, float maxX, float maxY)
	{
		Min = new Vector2(minX, minY);
		Max = new Vector2(maxX, maxY);
	}
	
	public Vector2 Min;
	public Vector2 Max;

	/// <summary>
	/// An empty <see cref="Rect"/>, represented by maximum and minimum corner points
	/// at <c>Vector2(float.NegativeInfinity)</c> and <c>Vector2(float.PositiveInfinity)</c>, respectively.
	/// This is so the <see cref="Rect"/> has an infinitely negative size.
	/// This is useful, because when taking a union B of a non-empty <see cref="Rect"/> A and
	/// this empty <see cref="Rect"/>, B will simply equal A.
	/// </summary>
	public static readonly Rect Empty = new () {
		Max = new Vector2(float.NegativeInfinity, float.NegativeInfinity),
		Min = new Vector2(float.PositiveInfinity, float.PositiveInfinity)
	};

	/// <summary>
	/// Create a new rectangle from its center and size.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Thrown if any of the components of the size is negative.
	/// </exception>
	/// <example>
	/// <code>
	/// var r = Rect.FromCenterSize(Vector2.Zero, Vector2.One); // w=1 h=1
	/// // r.Min is approximately (-0.5, -0.5)
	/// // r.Max is approximately (0.5, 0.5)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Rect FromCenterSize(Vector2 origin, Vector2 size)
	{
		if (size.X < 0f || size.Y < 0f)
			throw new ArgumentException("Rect size must be positive", nameof(size));

		var halfSize = size / 2f;
		return FromCenterHalfSize(origin, halfSize);
	}

	/// <summary>
	/// Create a new rectangle from its center and half-size.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Thrown if any of the components of the half-size is negative.
	/// </exception>
	/// <example>
	/// <code>
	/// var r = Rect.FromCenterHalfSize(Vector2.Zero, Vector2.One); // w=2 h=2
	/// // r.Min is approximately (-1, -1)
	/// // r.Max is approximately (1, 1)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Rect FromCenterHalfSize(Vector2 origin, Vector2 halfSize)
	{
		if (halfSize.X < 0f || halfSize.Y < 0f)
			throw new ArgumentException("Rect half_size must be positive", nameof(halfSize));

		return new Rect {
			Min = origin - halfSize,
			Max = origin + halfSize
		};
	}

	/// <summary>
	/// Check if the rectangle is empty.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.FromCorners(Vector2.Zero, new Vector2(0f, 1f)); // w=0 h=1
	/// // r.IsEmpty() returns true
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool IsEmpty()
	{
		return Min.X >= Max.X || Min.Y >= Max.Y;
	}

	/// <summary>
	/// Rectangle width (Max.X - Min.X).
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.Width() returns 5
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly float Width()
	{
		return Max.X - Min.X;
	}

	/// <summary>
	/// Rectangle height (Max.Y - Min.Y).
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.Height() returns 1
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly float Height()
	{
		return Max.Y - Min.Y;
	}

	/// <summary>
	/// Rectangle size.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.Size() returns (5, 1)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector2 Size()
	{
		return Max - Min;
	}

	/// <summary>
	/// Rectangle half-size.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.HalfSize() returns (2.5, 0.5)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector2 HalfSize()
	{
		return Size() * 0.5f;
	}

	/// <summary>
	/// The center point of the rectangle.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.Center() returns (2.5, 0.5)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Vector2 Center()
	{
		return (Min + Max) * 0.5f;
	}

	/// <summary>
	/// Check if a point lies within this rectangle, inclusive of its edges.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// // r.Contains(r.Center()) returns true
	/// // r.Contains(r.Min) returns true
	/// // r.Contains(r.Max) returns true
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Contains(Vector2 point)
	{
		return point.X >= Min.X && point.X <= Max.X &&
		       point.Y >= Min.Y && point.Y <= Max.Y;
	}

	/// <summary>
	/// Build a new rectangle formed of the union of this rectangle and another rectangle.
	/// </summary>
	/// <remarks>
	/// The union is the smallest rectangle enclosing both rectangles.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r1 = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// var r2 = Rect.New(1f, -1f, 3f, 3f); // w=2 h=4
	/// var r = r1.Union(r2);
	/// // r.Min is approximately (0, -1)
	/// // r.Max is approximately (5, 3)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Union(Rect other)
	{
		return new Rect {
			Min = Vector2.Min(Min, other.Min),
			Max = Vector2.Max(Max, other.Max)
		};
	}

	/// <summary>
	/// Build a new rectangle formed of the union of this rectangle and a point.
	/// </summary>
	/// <remarks>
	/// The union is the smallest rectangle enclosing both the rectangle and the point. If the
	/// point is already inside the rectangle, this method returns a copy of the rectangle.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// var u = r.UnionPoint(new Vector2(3f, 6f));
	/// // u.Min is approximately (0, 0)
	/// // u.Max is approximately (5, 6)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect UnionPoint(Vector2 other)
	{
		return new Rect {
			Min = Vector2.Min(Min, other),
			Max = Vector2.Max(Max, other)
		};
	}

	/// <summary>
	/// Build a new rectangle formed of the intersection of this rectangle and another rectangle.
	/// </summary>
	/// <remarks>
	/// The intersection is the largest rectangle enclosed in both rectangles. If the intersection
	/// is empty, this method returns an empty rectangle (<see cref="IsEmpty"/> returns <c>true</c>), but
	/// the actual values of <see cref="Min"/> and <see cref="Max"/> are implementation-dependent.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r1 = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// var r2 = Rect.New(1f, -1f, 3f, 3f); // w=2 h=4
	/// var r = r1.Intersect(r2);
	/// // r.Min is approximately (1, 0)
	/// // r.Max is approximately (3, 1)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Intersect(Rect other)
	{
		var min = Vector2.Max(Min, other.Min);
		var max = Vector2.Min(Max, other.Max);
		// Collapse min over max to enforce invariants and ensure e.g. Width() or
		// Height() never return a negative value.
		min = Vector2.Min(min, max);
		return new Rect { Min = min, Max = max };
	}

	/// <summary>
	/// Create a new rectangle by expanding it evenly on all sides.
	/// </summary>
	/// <remarks>
	/// A positive expansion value produces a larger rectangle,
	/// while a negative expansion value produces a smaller rectangle.
	/// If this would result in zero or negative width or height, <see cref="Empty"/> is returned instead.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r = Rect.New(0f, 0f, 5f, 1f); // w=5 h=1
	/// var r2 = r.Inflate(3f); // w=11 h=7
	/// // r2.Min is approximately (-3, -3)
	/// // r2.Max is approximately (8, 4)
	///
	/// var r = Rect.New(0f, -1f, 6f, 7f); // w=6 h=8
	/// var r2 = r.Inflate(-2f); // w=2 h=4
	/// // r2.Min is approximately (2, 1)
	/// // r2.Max is approximately (4, 5)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Inflate(float expansion)
	{
		var min = Min - new Vector2(expansion);
		var max = Max + new Vector2(expansion);
		// Collapse min over max to enforce invariants and ensure e.g. Width() or
		// Height() never return a negative value.
		min = Vector2.Min(min, max);
		return new Rect { Min = min, Max = max };
	}

	/// <summary>
	/// Build a new rectangle from this one with its coordinates expressed
	/// relative to <paramref name="other"/> in a normalized ([0..1] x [0..1]) coordinate system.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = Rect.New(2f, 3f, 4f, 6f);
	/// var s = Rect.New(0f, 0f, 10f, 10f);
	/// var n = r.Normalize(s);
	/// // n.Min.X == 0.2
	/// // n.Min.Y == 0.3
	/// // n.Max.X == 0.4
	/// // n.Max.Y == 0.6
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Normalize(Rect other)
	{
		var outerSize = other.Size();
		return new Rect {
			Min = (Min - other.Min) / outerSize,
			Max = (Max - other.Min) / outerSize
		};
	}

	/// <summary>
	/// Returns this rectangle as a <see cref="URect"/> (uint).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly URect AsURect()
	{
		return URect.FromCorners(new UVec2((uint)Min.X, (uint)Min.Y), new UVec2((uint)Max.X, (uint)Max.Y));
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator URect(Rect r) => r.AsURect();
}