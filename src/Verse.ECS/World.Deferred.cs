using System.Collections.Concurrent;

namespace Verse.ECS;

public sealed partial class World
{
	public void ApplyCommandBuffer(CommandBuffer buffer)
	{
		ApplyOperations(buffer._operations);
	}

	public void ApplyOperations(Queue<ICommand> queue)
	{
		lock (this) {
			Merge(queue);
		}
	}

	private void Merge(Queue<ICommand> queue)
	{
		while (queue.TryDequeue(out var op)) {
			op.Apply(this);
		}
	}
}