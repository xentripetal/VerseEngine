namespace Verse.Math;

public struct UVec2
{
	public uint X, Y;

	public UVec2(uint x, uint y)
	{
		X = x;
		Y = y;
	}

	public static UVec2 Zero => new (0, 0);
	public static UVec2 One => new (1, 1);
	public static UVec2 UnitX => new (1, 0);
	public static UVec2 UnitY => new (0, 1);

	public static UVec2 operator +(UVec2 a, UVec2 b) => new (a.X + b.X, a.Y + b.Y);
	public static UVec2 operator -(UVec2 a, UVec2 b) => new (a.X - b.X, a.Y - b.Y);
	public static UVec2 operator *(UVec2 a, UVec2 b) => new (a.X * b.X, a.Y * b.Y);
	public static UVec2 operator /(UVec2 a, UVec2 b) => new (a.X / b.X, a.Y / b.Y);

	public static UVec2 operator +(UVec2 a, uint b) => new (a.X + b, a.Y + b);
	public static UVec2 operator -(UVec2 a, uint b) => new (a.X - b, a.Y - b);
	public static UVec2 operator *(UVec2 a, uint b) => new (a.X * b, a.Y * b);
	public static UVec2 operator /(UVec2 a, uint b) => new (a.X / b, a.Y / b);
	public static UVec2 operator *(uint b, UVec2 a) => new (a.X * b, a.Y * b);
}