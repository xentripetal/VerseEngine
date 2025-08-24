namespace Verse.ECS;

public class PtrMutationData
{
	public bool Mutated;
}

[SkipLocalsInit]
public ref struct Ptr<T> where T : struct
{
	internal ref T Value;
	internal PtrMutationData? MutationData;

	public unsafe bool Mutated {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => MutationData is { Mutated: true };
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set {
			if (MutationData != null)
				MutationData.Mutated = value;
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	public ref T Mut {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			Mutated = true;
			return ref Value;
		}
	}

	public readonly ref readonly T Ref {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref Value;
	}

	public readonly bool IsValid() => !Unsafe.IsNullRef(ref Value);
}

[SkipLocalsInit]
public readonly ref struct PtrRO<T> where T : struct
{
	public PtrRO(ref readonly T r)
	{
		Ref = ref r;
	}

	public readonly ref readonly T Ref;
}

[SkipLocalsInit]
internal ref struct DataRow<T> where T : struct
{
	public Ptr<T> Value;
	public nint Size;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Next()
	{
		Value.Value = ref Unsafe.AddByteOffset(ref Value.Value, Size);
		Value.Mutated = false;
	}
}