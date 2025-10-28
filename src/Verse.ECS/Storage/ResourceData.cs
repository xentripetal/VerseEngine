using System.Collections;

namespace Verse.ECS;

/// <summary>
/// Type erased backing storage and metadata for a single resource within a <see cref="World"/>
/// </summary>
public class ResourceData
{
	public ResourceData()
	{
		data = null;
		addedTick = new BoxedTick();
		changedTick = new BoxedTick();
	}

	// Bevy uses an array, similar to our table storage. Might want to go that route.
	private object? data;
	private BoxedTick addedTick;
	private BoxedTick changedTick;

	public bool IsPresent => data != null;

	public T Get<T>() 
	{
		if (data is T typedData) {
			return typedData;
		}
		throw new InvalidCastException($"Resource is of type {data?.GetType().FullName ?? "null"}, cannot cast to {typeof(T).FullName}");
	}
	
	public object? Get()
	{
		return data;
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
	public void Insert(object? value, Tick changeTick)
	{
		if (!IsPresent) {
			addedTick = changeTick;
		}
		data = value;
		changedTick = changeTick;
	}

	public void InsertWithTicks(object? value, ResourceTicks ticks)
	{
		data = value;
		addedTick = ticks.Added;
		changedTick = ticks.Changed;
	}

	/// <summary>
	/// Removes a value from the resource, if present
	/// </summary>
	public (object, ResourceTicks)? Remove()
	{
		var existing = data;
		if (IsPresent) {
			data = null;
			return (existing!, new ResourceTicks(addedTick, changedTick));
		}
		return null;
	}

	public void RemoveAndDispose()
	{
		if (!IsPresent) {
			return;
		}
		if (data is IDisposable disposable) {
			disposable.Dispose();
		}
		data = null;
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
	private SparseSet<ComponentId, ResourceData> resources = new ();

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
		return resources.GetOrInsert(id, () => new ResourceData());
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void CheckChangeTicks(Tick check)
	{
		foreach (var v in resources.Values) {
			v.CheckChangeTicks(check);
		}
	}
}