using System.Numerics;
using System.Runtime.Intrinsics;

namespace Verse.Math;

public struct UVec4
{
	private Vector128<uint> _vector;

	public UVec4(Vector128<uint> vector)
	{
		_vector = vector;
	}

	public UVec4(uint x, uint y, uint z, uint w)
	{
		_vector = Vector128.Create(x, y, z, w);
	}

	public static UVec4 Zero => new (0u, 0u, 0u, 0u);
	public static UVec4 One => new (1, 1, 1, 1);
	public static UVec4 UnitX => new (1, 0, 0, 0);
	public static UVec4 UnitY => new (0, 1, 0, 0 );
	
	public static UVec4 operator +(UVec4 a, UVec4 b) => new UVec4(a._vector + b._vector);
	public static UVec4 operator -(UVec4 a, UVec4 b) => new UVec4(a._vector - b._vector);
	public static UVec4 operator *(UVec4 a, UVec4 b) => new UVec4(a._vector * b._vector);
	public static UVec4 operator /(UVec4 a, UVec4 b) => new UVec4(a._vector / b._vector);

	public static UVec4 operator +(UVec4 a, uint b) => new UVec4(a._vector + Vector128.Create(b));
	public static UVec4 operator -(UVec4 a, uint b) => new UVec4(a._vector - Vector128.Create(b));
	public static UVec4 operator *(UVec4 a, uint b) => new UVec4(a._vector * Vector128.Create(b));
	public static UVec4 operator /(UVec4 a, uint b) => new UVec4(a._vector / Vector128.Create(b));
	public static UVec4 operator *(uint b, UVec4 a) => new UVec4(a._vector * Vector128.Create(b));
}