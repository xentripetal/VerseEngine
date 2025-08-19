namespace Verse.ECS.Systems;

public class ExampleSystemSet
{
	public void ExampleSystem(World world) { }

	public void OnInit(Query<Data<int>> query) { }

	public void OnUpdate(Query<Data<int>> query) { }
	public void OnDestroy(Query<Data<int>> query) { }
}

public class NonSingletonSystemSet
{
	public enum Systems
	{
		OnInit, OnUpdate, OnDestroy
	}

	public void OnInit(Query<Data<int>> query)
	{
		Delegate a = this.OnDestroy;
	}

	public void OnUpdate(Query<Data<int>> query) { }
	public void OnDestroy(Query<Data<int>> query) { }
}