using System.Collections;

namespace Verse.ECS;

/// <summary>
/// Type erased backing storage and metadata for a single resource within a <see cref="World"/>
/// </summary>
public class ResourceData
{
	public ResourceData(Array data)
	{
		this.data = data;
		addedTick = new BoxedTick();
		changedTick = new BoxedTick();
	}

	private bool hasValue;
	private Array data;
	private BoxedTick addedTick;
	private BoxedTick changedTick;

	public bool IsPresent => hasValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Get<T>() {
		if (!IsPresent) {
			throw new InvalidOperationException("Resource is not present");
		}
		var span = new Span<T>(Unsafe.As<T[]>(data), 0, 1);
		return MemoryMarshal.GetReference(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ref T GetRef<T>()
	{
		if (!IsPresent) {
			throw new InvalidOperationException("Resource is not present");
		}
		var span = new Span<T>(Unsafe.As<T[]>(data), 0, 1);
		return ref MemoryMarshal.GetReference(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<T> GetSpan<T>()
	{
		if (!IsPresent) {
			throw new InvalidOperationException("Resource is not present");
		}
		return new Span<T>(Unsafe.As<T[]>(data), 0, 1);
	}
	
	public ResourceTicks? GetTicks()
	{
		if (IsPresent) {
			return new ResourceTicks(addedTick, changedTick);
		}
		return null;
	}

	public (object, ResourceTicks)? GetWithTicks()
	{
		if (IsPresent) {
			return (data!, new ResourceTicks(addedTick, changedTick));
		}
		return null;
	}


	/// <summary>
	/// Inserts a value into the resource. If a value is already present, it will be replaced
	/// </summary>
	/// <param name="value">Value to insert. Must be valid for the underlying type for the resource</param>
	/// <param name="changeTick">Current change tick of the world</param>
	public void Insert<T>(T value, Tick changeTick)
	{
		if (!IsPresent) {
			addedTick = changeTick;
		}
		hasValue = true;
		GetRef<T>() = value;
		changedTick = changeTick;
	}

	public void InsertWithTicks<T>(T value, ResourceTicks ticks)
	{
		hasValue = true;
		GetRef<T>() = value;
		addedTick = ticks.Added;
		changedTick = ticks.Changed;
	}

	/// <summary>
	/// Removes a value from the resource, if present
	/// </summary>
	public (object?, ResourceTicks)? Remove()
	{
		if (IsPresent) {
			var value = data.GetValue(0);
			Array.Clear(data, 0 , 1);
			hasValue = false;
			return (value, new ResourceTicks(addedTick, changedTick));
		}
		return null;
	}

	public void RemoveAndDispose()
	{
		if (!IsPresent) {
			return;
		}
		var value = data.GetValue(0);
		if (value is IDisposable disposable) {
			disposable.Dispose();
		}
		Array.Clear(data, 0 , 1);
		hasValue = false;
	}

	public void CheckChangeTicks(Tick check)
	{
		addedTick.Tick.CheckTick(check);
		changedTick.Tick.CheckTick(check);
	}
}

/// <summary>
/// The backing store for all <see cref="Resources"/> stored in the <see cref="World"/>
/// </summary>
public class Resources : IEnumerable<KeyValuePair<ComponentId, ResourceData>>
{
	public Resources(ComponentRegistry registry)
	{
		this.registry = registry;
	}
	private SparseSet<ComponentId, ResourceData> resources = new ();
	private ComponentRegistry registry;

	public int Length => resources.Length;

	public IEnumerator<KeyValuePair<ComponentId, ResourceData>> GetEnumerator() => resources.GetEnumerator();

	public bool IsEmpty => resources.Length == 0;

	public ResourceData? Get(ComponentId componentId)
	{
		return resources.Get(componentId);
	}

	public void Clear()
	{
		resources.Clear();
	}

	public ResourceData InitializeResource(ComponentId id)
	{
		// Bevy does some complicated set up here. we just do boxing as an object. Think it should have the same side affects?
		return resources.GetOrInsert(id, () => new ResourceData(registry.GetArray(id, 1) ?? throw new InvalidOperationException($"Failed to create resource array for resource {id}")));
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void CheckChangeTicks(Tick check)
	{
		foreach (var v in resources.Values) {
			v.CheckChangeTicks(check);
		}
	}
}