namespace Verse.Math;

public struct TRange<T> where T : IComparable<T>
{
	public T Start;
	public T End;

	public TRange(T start, T end)
	{
		Start = start;
		End = end;
	}

	public TRange(T value)
	{
		Start = value;
		End = value;
	}
	
	public TRange(TRange<T> range)
	{
		Start = range.Start;
		End = range.End;
	}

	public bool Contains(T value)
	{
		return value.CompareTo(Start) >= 0 && value.CompareTo(End) <= 0;
	}

	public bool IsEmpty()
	{
		return Start.CompareTo(End) < 0;
	}
}