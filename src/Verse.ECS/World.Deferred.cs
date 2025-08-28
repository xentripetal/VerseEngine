using System.Collections.Concurrent;

namespace Verse.ECS;

public sealed partial class World
{
	public void ApplyCommandBuffer(CommandBuffer buffer)
	{
		lock (this) {
			Merge(buffer._operations);
		}
	}

	public void ApplyOperations(Queue<DeferredOp> queue)
	{
		lock (this) {
			Merge(queue);
		}
	}


	private void Merge(Queue<DeferredOp> queue)
	{
		while (queue.TryDequeue(out var op)) {
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
}