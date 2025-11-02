namespace Verse.ECS;

public partial class World
{
	public ComponentId RegisterResource<T>()
	{
		return Registry.RegisterResource<T>();
	}

	public ComponentId? ResourceId<T>()
	{
		return Registry.ResourceId<T>();
	}

	public ComponentId InitWorldResource<T>() where T : IFromWorld<T>
	{
		var id = RegisterResource<T>();
		var data = Resources.InitializeResource(id);
		if (!data.IsPresent) {
			data.Insert(T.FromWorld(this), ChangeTick());
		}
		return id;
	}
	
	public ComponentId InitResource<T>() where T : new()
	{
		var id = RegisterResource<T>();
		var data = Resources.InitializeResource(id);
		if (!data.IsPresent) {
			data.Insert(new T(), ChangeTick());
		}
		return id;
	}

	/// <summary>
	/// Will ensure the given resource is registered and if it does not already have a value,
	/// will set it to the provided value.
	/// </summary>
	/// <param name="value"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public ComponentId InitResource<T>(T value)
	{
		var id = RegisterResource<T>();
		var data = Resources.InitializeResource(id);
		if (!data.IsPresent) {
			data.Insert(value, ChangeTick());
		}
		return id;	
	}
	
	public void InsertResource<T>(T resource)
	{
		var id = RegisterResource<T>();
		var data = Resources.InitializeResource(id);
		data.Insert(resource, ChangeTick());
	}

	public T? RemoveResource<T>()
	{
		var id = Registry.ResourceId<T>();
		if (id == null)
			return default;
		var data = Resources.Get(id.Value);
		if (data == null)
			return default;
		var removed = data.Remove();
		if (removed != null) {
			return (T)removed.Value.Item1;
		}
		return default;
	}

	public bool ContainsResource<T>()
	{
		var id = Registry.ResourceId<T>();
		if (id == null)
			return false;
		var data = Resources.Get(id.Value);
		if (data == null)
			return false;
		return data.IsPresent;
	}

	public T Resource<T>()
	{
		var res = GetResource<T>();
		EcsAssert.Panic(res != null, $"resource of type {typeof(T)} not found in world");
		return res!;
	}

	public T? GetResource<T>()
	{
		var id = Registry.ResourceId<T>();
		if (id == null)
			return default;
		var data = Resources.Get(id.Value);
		if (data == null || !data.IsPresent)
			return default;
		return data.Get<T>();
	}
	
	public T GetResourceOrDefault<T>() where T : new() 
	{
		var res = GetResource<T>();
		if (res == null) {
			res = new T();
			InsertResource(res);
		}
		return res;
	}

	public void ResourceScope<T>(Action<T> fn)
	{
		var resMut = GetResource<T>();
		// TODO take the res out of the world for this fn
		fn(resMut);
	}
	public bool HasResource<T>()
	{
		var id = Registry.ResourceId<T>();
		if (id == null)
			return false;
		var data = Resources.Get(id.Value);
		if (data == null)
			return false;
		return data.IsPresent;
	}
}