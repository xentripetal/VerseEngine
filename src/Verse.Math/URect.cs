using System.Runtime.CompilerServices;

namespace Verse.Math;

/// <summary>
/// A rectangle defined by two opposite corners with unsigned integer coordinates.
/// The rectangle is represented by its minimum and maximum corner points.
/// </summary>
public struct URect
{
	/// <summary>
	/// The minimum corner point of the rect
	/// </summary>
	public UVec2 Min;

	/// <summary>
	/// The maximum corner point of the rect
	/// </summary>
	public UVec2 Max;

	/// <summary>
	/// An empty URect, represented by maximum and minimum corner points
	/// with max == UVec2.Min and min == UVec2.Max, so the rect has an
	/// extremely large negative size.
	/// This is useful because when taking a union B of a non-empty URect A and
	/// this empty URect, B will simply equal A.
	/// </summary>
	public static readonly URect Empty = new URect(UVec2.Max, UVec2.Min);

	/// <summary>
	/// Create a new rectangle from minimum and maximum corner points.
	/// Note: The points are assumed to be ordered (min, max). Use FromCorners if ordering is uncertain.
	/// </summary>
	public URect(UVec2 min, UVec2 max)
	{
		Min = min;
		Max = max;
	}

	/// <summary>
	/// Create a new rectangle from two corner points.
	/// The two points do not need to be the minimum and/or maximum corners.
	/// They only need to be two opposite corners.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 4, 10, 6); // w=10 h=2
	/// var r = URect.New(2, 4, 5, 0);  // w=3 h=4
	/// </code>
	/// </example>
	public static URect New(uint x0, uint y0, uint x1, uint y1)
	{
		return FromCorners(new UVec2(x0, y0), new UVec2(x1, y1));
	}

	/// <summary>
	/// Create a new rectangle from two corner points.
	/// The two points do not need to be the minimum and/or maximum corners.
	/// They only need to be two opposite corners.
	/// </summary>
	/// <example>
	/// <code>
	/// // Unit rect from [0,0] to [1,1]
	/// var r = URect.FromCorners(UVec2.Zero, UVec2.One); // w=1 h=1
	/// // Same; the points do not need to be ordered
	/// var r = URect.FromCorners(UVec2.One, UVec2.Zero); // w=1 h=1
	/// </code>
	/// </example>
	public static URect FromCorners(UVec2 p0, UVec2 p1)
	{
		return new URect(p0.MinComponents(p1), p0.MaxComponents(p1));
	}

	/// <summary>
	/// Create a new rectangle from its center and size.
	/// </summary>
	/// <remarks>
	/// Rounding Behavior: If the size contains odd numbers they will be rounded down
	/// to the nearest whole number.
	/// </remarks>
	/// <exception cref="System.ArgumentException">
	/// Thrown if any of the components of the size is negative or if origin - (size / 2)
	/// results in any negatives.
	/// </exception>
	/// <example>
	/// <code>
	/// var r = URect.FromCenterSize(UVec2.One, UVec2.Splat(2)); // w=2 h=2
	/// // r.Min == UVec2.Splat(0)
	/// // r.Max == UVec2.Splat(2)
	/// </code>
	/// </example>
	public static URect FromCenterSize(UVec2 origin, UVec2 size)
	{
		var halfSize = size / 2;
		if (!origin.CompareGreaterOrEqual(halfSize).All())
		{
			throw new System.ArgumentException(
				$"Origin must always be greater than or equal to (size / 2) otherwise the rectangle is undefined! " +
				$"Origin was {origin} and size was {size}");
		}
		return FromCenterHalfSize(origin, halfSize);
	}

	/// <summary>
	/// Create a new rectangle from its center and half-size.
	/// </summary>
	/// <exception cref="System.ArgumentException">
	/// Thrown if any of the components of the half-size is negative or if origin - half_size
	/// results in any negatives.
	/// </exception>
	/// <example>
	/// <code>
	/// var r = URect.FromCenterHalfSize(UVec2.One, UVec2.One); // w=2 h=2
	/// // r.Min == UVec2.Splat(0)
	/// // r.Max == UVec2.Splat(2)
	/// </code>
	/// </example>
	public static URect FromCenterHalfSize(UVec2 origin, UVec2 halfSize)
	{
		if (!origin.CompareGreaterOrEqual(halfSize).All())
		{
			throw new System.ArgumentException(
				$"Origin must always be greater than or equal to half_size otherwise the rectangle is undefined! " +
				$"Origin was {origin} and half_size was {halfSize}");
		}
		return new URect(origin - halfSize, origin + halfSize);
	}

	/// <summary>
	/// Check if the rectangle is empty.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.FromCorners(UVec2.Zero, new UVec2(0, 1)); // w=0 h=1
	/// // r.IsEmpty() == true
	/// </code>
	/// </example>
	public bool IsEmpty()
	{
		return Min.CompareGreaterOrEqual(Max).Any();
	}

	/// <summary>
	/// Rectangle width (Max.X - Min.X).
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 5, 1); // w=5 h=1
	/// // r.Width() == 5
	/// </code>
	/// </example>
	public uint Width()
	{
		return Max.X - Min.X;
	}

	/// <summary>
	/// Rectangle height (Max.Y - Min.Y).
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 5, 1); // w=5 h=1
	/// // r.Height() == 1
	/// </code>
	/// </example>
	public uint Height()
	{
		return Max.Y - Min.Y;
	}

	/// <summary>
	/// Rectangle size.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 5, 1); // w=5 h=1
	/// // r.Size() == new UVec2(5, 1)
	/// </code>
	/// </example>
	public UVec2 Size()
	{
		return Max - Min;
	}

	/// <summary>
	/// Rectangle half-size.
	/// </summary>
	/// <remarks>
	/// Rounding Behavior: If the full size contains odd numbers they will be rounded down
	/// to the nearest whole number when calculating the half size.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 4, 2); // w=4 h=2
	/// // r.HalfSize() == new UVec2(2, 1)
	/// </code>
	/// </example>
	public UVec2 HalfSize()
	{
		return Size() / 2;
	}

	/// <summary>
	/// The center point of the rectangle.
	/// </summary>
	/// <remarks>
	/// Rounding Behavior: If the (Min + Max) contains odd numbers they will be rounded down
	/// to the nearest whole number when calculating the center.
	/// </remarks>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 4, 2); // w=4 h=2
	/// // r.Center() == new UVec2(2, 1)
	/// </code>
	/// </example>
	public UVec2 Center()
	{
		return (Min + Max) / 2;
	}

	/// <summary>
	/// Check if a point lies within this rectangle, inclusive of its edges.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 5, 1); // w=5 h=1
	/// // r.Contains(r.Center()) == true
	/// // r.Contains(r.Min) == true
	/// // r.Contains(r.Max) == true
	/// </code>
	/// </example>
	public bool Contains(UVec2 point)
	{
		return (point.CompareGreaterOrEqual(Min) & point.CompareLessOrEqual(Max)).All();
	}

	/// <summary>
	/// Build a new rectangle formed of the union of this rectangle and another rectangle.
	/// The union is the smallest rectangle enclosing both rectangles.
	/// </summary>
	/// <example>
	/// <code>
	/// var r1 = URect.New(0, 0, 5, 1); // w=5 h=1
	/// var r2 = URect.New(1, 0, 3, 8); // w=2 h=8
	/// var r = r1.Union(r2);
	/// // r.Min == new UVec2(0, 0)
	/// // r.Max == new UVec2(5, 8)
	/// </code>
	/// </example>
	public URect Union(URect other)
	{
		return new URect(
			Min.MinComponents(other.Min),
			Max.MaxComponents(other.Max)
		);
	}

	/// <summary>
	/// Build a new rectangle formed of the union of this rectangle and a point.
	/// The union is the smallest rectangle enclosing both the rectangle and the point.
	/// If the point is already inside the rectangle, this method returns a copy of the rectangle.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(0, 0, 5, 1); // w=5 h=1
	/// var u = r.UnionPoint(new UVec2(3, 6));
	/// // u.Min == UVec2.Zero
	/// // u.Max == new UVec2(5, 6)
	/// </code>
	/// </example>
	public URect UnionPoint(UVec2 other)
	{
		return new URect(
			Min.MinComponents(other),
			Max.MaxComponents(other)
		);
	}

	/// <summary>
	/// Build a new rectangle formed of the intersection of this rectangle and another rectangle.
	/// The intersection is the largest rectangle enclosed in both rectangles. If the intersection
	/// is empty, this method returns an empty rectangle (IsEmpty() returns true), but
	/// the actual values of Min and Max are implementation-dependent.
	/// </summary>
	/// <example>
	/// <code>
	/// var r1 = URect.New(0, 0, 2, 2); // w=2 h=2
	/// var r2 = URect.New(1, 1, 3, 3); // w=2 h=2
	/// var r = r1.Intersect(r2);
	/// // r.Min == new UVec2(1, 1)
	/// // r.Max == new UVec2(2, 2)
	/// </code>
	/// </example>
	public URect Intersect(URect other)
	{
		var r = new URect(
			Min.MaxComponents(other.Min),
			Max.MinComponents(other.Max)
		);
		// Collapse min over max to enforce invariants and ensure e.g. Width() or
		// Height() never return a negative value.
		r.Min = r.Min.MinComponents(r.Max);
		return r;
	}

	/// <summary>
	/// Create a new rectangle by expanding it evenly on all sides.
	/// A positive expansion value produces a larger rectangle,
	/// while a negative expansion value produces a smaller rectangle.
	/// If this would result in zero width or height, Empty is returned instead.
	/// </summary>
	/// <example>
	/// <code>
	/// var r = URect.New(4, 4, 6, 6); // w=2 h=2
	/// var r2 = r.Inflate(1); // w=4 h=4
	/// // r2.Min == UVec2.Splat(3)
	/// // r2.Max == UVec2.Splat(7)
	///
	/// var r = URect.New(4, 4, 8, 8); // w=4 h=4
	/// var r2 = r.Inflate(-1); // w=2 h=2
	/// // r2.Min == UVec2.Splat(5)
	/// // r2.Max == UVec2.Splat(7)
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public URect Inflate(int expansion)
	{
		var r = new URect(
			Min.SaturatingAddSigned(-expansion),
			Max.SaturatingAddSigned(expansion)
		);
		// Collapse min over max to enforce invariants and ensure e.g. Width() or
		// Height() never return a negative value.
		r.Min = r.Min.MinComponents(r.Max);
		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Rect ToRect() => new Rect(Min.AsVector2(), Max.AsVector2());
	// public Rect AsRect() { ... }
	// public IRect AsIRect() { ... }
}