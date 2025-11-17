namespace Verse.ECS;

public class EventRegistry
{
	protected readonly List<IEventParam> EventParams = new List<IEventParam>();
	public void Clear()
	{
		EventParams.Clear();
	}

	/// <summary>
	/// Updates all the registered events in the world
	/// </summary>
	/// <param name="world"></param>
	/// <param name="tick"></param>
	public void Update(World world, uint tick)
	{
		foreach (var ev in EventParams) {
			ev.Clear();
		}
	}

	internal void Register<T>(Messages<T> ev) where T : notnull
	{
		EventParams.Add(ev);
	}
}