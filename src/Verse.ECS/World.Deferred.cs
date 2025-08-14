using System.Collections.Concurrent;

namespace Verse.ECS;

public sealed partial class World
{
	private readonly ConcurrentQueue<DeferredOp> _operations = new ConcurrentQueue<DeferredOp>();
	private WorldState _worldState = new WorldState { Locks = 0 };

	public bool IsDeferred => _worldState.Locks > 0;
	public bool IsMerging => _worldState.Locks < 0;




	public void BeginDeferred()
	{
		if (IsMerging)
			return;

		var locks = _worldState.Begin();
		EcsAssert.Assert(locks > 0, "");
	}


	public void EndDeferred()
	{
		if (IsMerging)
			return;

		var locks = _worldState.End();
		EcsAssert.Assert(locks >= 0, "");

		if (locks == 0 && _operations.Count > 0) {
			_worldState.Lock();
			Merge();
			_worldState.Unlock();
		}
	}


	internal void AddDeferred<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetComponent,
			Entity = entity,
			Data = null!,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	internal ref T SetDeferred<T>(EcsID entity, T component) where T : struct
	{
		ref readonly var cmp = ref GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetComponent,
			Entity = entity,
			Data = component,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);

		return ref Unsafe.Unbox<T>(cmd.Data);
	}

	internal object? SetDeferred(EcsID entity, EcsID id, object? rawCmp, int size)
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

	internal void SetChangedDeferred<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetChanged,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	internal void SetChangedDeferred(EcsID entity, EcsID id)
	{
		var cmp = new SlimComponent(id, -1);

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.SetChanged,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	internal void UnsetDeferred<T>(EcsID entity) where T : struct
	{
		ref readonly var cmp = ref GetComponent<T>();

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.UnsetComponent,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	internal void UnsetDeferred(EcsID entity, EcsID id)
	{
		var cmp = new SlimComponent(id, 0);

		var cmd = new DeferredOp {
			Op = DeferredOpTypes.UnsetComponent,
			Entity = entity,
			SlimComponent = cmp
		};

		_operations.Enqueue(cmd);
	}

	internal void DeleteDeferred(EcsID entity)
	{
		var cmd = new DeferredOp {
			Op = DeferredOpTypes.DestroyEntity,
			Entity = entity
		};

		_operations.Enqueue(cmd);
	}


	private void Merge()
	{
		while (_operations.TryDequeue(out var op)) {
			switch (op.Op) {
				case DeferredOpTypes.DestroyEntity:
					if (Exists(op.Entity))
						Delete(op.Entity);
					break;

				case DeferredOpTypes.SetComponent:
					{
					var (array, row) = Attach(op.Entity, in op.SlimComponent);
					array?.SetValue(op.Data, row & ECS.Archetype.CHUNK_THRESHOLD);

					break;
					}

				case DeferredOpTypes.UnsetComponent:
					{
					Detach(op.Entity, op.SlimComponent.Id);

					break;
					}
				case DeferredOpTypes.SetChanged:
					{
					if (Exists(op.Entity) && Has(op.Entity, op.SlimComponent.Id))
						SetChanged(op.Entity, op.SlimComponent.Id);
					break;
					}
			}
		}

	}


	private struct WorldState
	{
		public int Locks;

		public int Lock() => Locks = -1;
		public int Unlock() => Locks = 0;
		public int Begin() => Interlocked.Increment(ref Locks);
		public int End() => Interlocked.Decrement(ref Locks);
	}

	private struct DeferredOp
	{
		public DeferredOpTypes Op;
		public EcsID Entity;
		public SlimComponent SlimComponent;
		public object? Data;
	}

	private enum DeferredOpTypes : byte
	{
		DestroyEntity,
		SetComponent,
		UnsetComponent,
		SetChanged
	}
}