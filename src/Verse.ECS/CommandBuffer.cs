namespace Verse.ECS;

public class CommandBuffer
{
	private World _world;
	internal readonly Queue<DeferredOp> _operations;
	public CommandBuffer(World world)
	{
		_world = world;
		_operations = new Queue<DeferredOp>();
	}

	public void Add<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref _world.GetComponent<T>();
		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetComponent,
			Entity = entity,
			Data = null!,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	public ref T Set<T>(EcsID entity, T component) where T : struct
	{
		ref readonly var cmp = ref _world.GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetComponent,
			Entity = entity,
			Data = component,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);

		return ref Unsafe.Unbox<T>(cmd.Data);
	}

	public object? Set(EcsID entity, EcsID id, object? rawCmp, int size)
	{
		var cmp = new SlimComponent(id, size);

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetComponent,
			Entity = entity,
			Data = rawCmp,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
		return rawCmp;
	}

	public void SetChanged<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref _world.GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetChanged,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	public void SetChanged(EcsID entity, EcsID id)
	{
		var cmp = new SlimComponent(id, -1);

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetChanged,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	public void Unset<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref _world.GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.UnsetComponent,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	public void Unset(EcsID entity, EcsID id)
	{
		var cmp = new SlimComponent(id, 0);

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.UnsetComponent,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	public void Delete(EcsID entity)
	{
		var cmd = new DeferredOp {
			Op = DeferredOpTypes.DestroyEntity,
			Entity = entity
		};

		_operations.Enqueue(cmd);
	}

}

public struct DeferredOp
{
	public DeferredOpTypes Op;
	public EcsID Entity;
	public SlimComponent SlimComponent;
	public object? Data;
}

public enum DeferredOpTypes : byte
{
	DestroyEntity,
	SetComponent,
	UnsetComponent,
	SetChanged
}