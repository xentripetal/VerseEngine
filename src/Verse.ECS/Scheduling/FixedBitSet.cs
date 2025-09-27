using System.Collections;

namespace Verse.ECS.Scheduling;

public struct FixedBitSet : IEquatable<FixedBitSet>, IEnumerable<ulong>
{
	public FixedBitSet(int length = 0)
	{
		var capacity = RoundCapacity(length);
		EnsureCapacity(capacity);
		Length = length;
	}

	private BitArray? _data;
	public int Capacity { get; private set; }
	public int Length { get; private set; }

	/// Returns `true` if `self` has no elements in common with `other`. This
	/// is equivalent to checking for an empty intersection.
	public bool IsDisjoint(FixedBitSet other) => !Clone().And(other).HasAnySet();

	public bool HasAnySet() => _data != null && _data.HasAnySet();

	private static int GetInt32ArrayLengthFromBitLength(int n) => n - 1 + 32 >>> 5;

	private static int RoundCapacity(int capacity) => capacity + 31 & ~31;

	public void EnsureCapacity(int capacity)
	{
		if (capacity > Capacity) {
			var targetCapacity = RoundCapacity(Math.Max(Capacity * 2, capacity));
			var newInts = new int[GetInt32ArrayLengthFromBitLength(targetCapacity)];
			_data?.CopyTo(newInts, 0);
			var newData = new BitArray(newInts);
			_data = newData;
			Capacity = _data.Length;
		}
	}

	public void Set(int index)
	{
		SetValue(index, true);
	}
	
	public void Set(ulong index)
	{
		SetValue((int)index, true);
	}

	public void SetValue(int index, bool value)
	{
		EnsureCapacity(index + 1);
		_data!.Set(index, value);
		Length = Math.Max(Length, index + 1);
	}

	/// Return **true** if the bit is enabled in the **FixedBitSet**,
	/// **false** otherwise.
	///
	/// Note: bits outside the capacity are always disabled.
	///
	/// Note: Also available with index syntax: `bitset[bit]`.
	public bool Contains(int bit) => bit < Length && _data!.Get(bit);

	public bool Get(int index)
	{
		if (index >= Length) {
			throw new IndexOutOfRangeException();
		}
		return _data!.Get(index);
	}


	public bool this[int index] {
		get => Get(index);
		set => SetValue(index, value);
	}
	
	public bool this[ulong index] {
		get => Get((int)index);
		set => SetValue((int) index, value);
	}

	public void Clear()
	{
		_data?.SetAll(false);
	}

	public void Or(FixedBitSet other)
	{
		if (other.Length > Length) {
			EnsureCapacity(other.Capacity);
			Length = other.Length;
		} else if (Length > other.Length) {
			other.EnsureCapacity(Capacity);
			other.Length = Length;
		}
		_data = _data?.Or(other._data!);
	}
	
	public bool Overlaps(FixedBitSet other)
	{
		var smaller = this;
		var larger = other;
		if (other.Length < Length) {
			smaller = other;
			larger = this;
		}

		smaller = smaller.Clone();
		smaller.EnsureCapacity(larger.Capacity);
		smaller.Length = larger.Length;

		return smaller.And(larger).HasAnySet();
	}

	public FixedBitSet And(FixedBitSet other)
	{
		if (other.Length > Length) {
			EnsureCapacity(other.Capacity);
			Length = other.Length;
		} else if (Length > other.Length) {
			other.EnsureCapacity(Capacity);
			other.Length = Length;
		}

		_data = _data?.And(other._data!);
		return this;
	}

	public FixedBitSet Clone()
	{
		var clone = new FixedBitSet(Length);
		if (_data != null) clone._data = new BitArray(_data);
		return clone;
	}

	/// Iterates over all enabled bits.
	///
	/// Iterator element is the index of the `1` bit, type `usize`.
	public IEnumerable<int> Ones()
	{
		int il = Length;
		for (var i = 0; i < il; i++) {
			if (_data![i]) {
				yield return i;
			}
		}
	}
	
	/// Iterates over all enabled bits.
	///
	/// Iterator element is the index of the `1` bit, type `usize`.
	public IEnumerable<int> Zeros()
	{
		int il = Length;
		for (var i = 0; i < il; i++) {
			if (!_data![i]) {
				yield return i;
			}
		}
	}
	
	/// Iterates over all disabled bits in reverse order.
	///
	/// Iterator element is the index of the `0` bit, type `usize`.
	public IEnumerable<int> ZerosReverse()
	{
		for (var i = Length-1; i >= 0; i--) {
			if (!_data![i]) {
				yield return i;
			}
		}
	}
	
	/// Iterates over all enabled bits.
	///
	/// Iterator element is the index of the `1` bit, type `usize`.
	public IEnumerable<ulong> OnesUL()
	{
		int il = Length;
		for (var i = 0; i < il; i++) {
			if (_data![i]) {
				yield return (ulong)i;
			}
		}
	}
	
	
	public bool Equals(FixedBitSet other)
	{
		if (Length != other.Length)
			return false;
		if (Length == 0) 
			return true;
		return !((BitArray)_data!.Clone()).Xor(other._data!).HasAnySet();
	}

	public IEnumerator<ulong> GetEnumerator()
	{
		return OnesUL().GetEnumerator();
	}
	
	public override bool Equals(object? obj) => obj is FixedBitSet other && Equals(other);
	public override int GetHashCode() => HashCode.Combine(_data, Capacity, Length);
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public bool IsSubsetOf(FixedBitSet other)
	{
		if (Length > other.Length)
			return false;
		
		var clone = Clone();
		clone.EnsureCapacity(other.Capacity);
		clone.Length = other.Length;
		
		clone.And(other);
		return Equals(clone);
	}
}
