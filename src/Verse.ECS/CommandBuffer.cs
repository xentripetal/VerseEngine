namespace Verse.ECS;

public class CommandBuffer
{
	private World _world;
	internal readonly Queue<ICommand> _operations;
	public CommandBuffer(World world)
	{
		_world = world;
		_operations = new Queue<ICommand>();
	}
	
	public void AddCommand(ICommand command)
	{
		_operations.Enqueue(command);
	}

	public void Set<T>(EcsID entity, T component) 
	{
		AddCommand(new SetComponentCommand<T>(entity, component));
	}

	public void SetChanged<T>(EcsID entity) where T : struct
	{
		AddCommand(new SetChangedCommand<T>(entity));
	}

	public void Unset<T>(EcsID entity) 
	{
		AddCommand(new UnsetComponentCommand<T>(entity));
	}

	public void Delete(EcsID entity)
	{
		AddCommand(new DeleteEntityCommand { Entity = entity });
	}

	public void InsertResource<T>(T resource) 
	{
		AddCommand(new InsertResourceCommand<T>(resource));
	}
}

public interface ICommand
{
	public void Apply(World world);
}

public record struct SetComponentCommand<T>(EcsID Entity, T Component) : ICommand
{
	public void Apply(World world) => world.Entity(Entity).Set(Component);
}

public record struct DeleteEntityCommand : ICommand
{
	public EcsID Entity;
	public void Apply(World world) => world.Delete(Entity);
}

public record struct UnsetComponentCommand<T>(EcsID Entity) : ICommand
{
	public void Apply(World world) => world.Entity(Entity).Unset<T>();
}

public record struct SetChangedCommand<T>(EcsID Entity) : ICommand
	where T : struct
{
	public void Apply(World world) => world.SetChanged<T>(Entity);
}

public record struct InsertResourceCommand<T>(T Resource) : ICommand
{
	public void Apply(World world) => world.InsertResource(Resource);
}