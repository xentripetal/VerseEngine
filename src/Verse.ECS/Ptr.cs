namespace Verse.ECS;

[SkipLocalsInit]
public ref struct Ptr<T> where T : struct
{
	public ref T Value;
	
	public readonly ref readonly T Ref => ref Value;
	public ref T Mut => ref Value;
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
public ref struct Cell<T>
{
	internal ref T Value;
	internal ref Tick AddedTick;
	internal ref Tick ChangedTick;
	internal Tick LastRun;
	internal Tick ThisRun;


	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	public ref T Mut {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			ChangedTick = ThisRun;
			return ref Value;
		}
	}

	public readonly ref readonly T Ref {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref Value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool IsValid() => !Unsafe.IsNullRef(ref Value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool IsChanged() => ChangedTick.IsNewerThan(LastRun, ThisRun);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool IsAdded() => AddedTick.IsNewerThan(LastRun, ThisRun);
}



[SkipLocalsInit]
internal ref struct DataRow<T>
{
	public Cell<T> Value;
	public nint Size;

	private static readonly int tickOffset = Unsafe.SizeOf<Tick>();


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Next()
	{
		Value.Value = ref Unsafe.AddByteOffset(ref Value.Value, Size);
		Value.AddedTick = ref Unsafe.AddByteOffset(ref Value.AddedTick, tickOffset);
		Value.ChangedTick = ref Unsafe.AddByteOffset(ref Value.ChangedTick, tickOffset);
	}
}