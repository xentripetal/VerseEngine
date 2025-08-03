using System.Collections.Concurrent;

namespace Verse.MoonWorks.Graphics;

internal class FencePool
{
	private readonly ConcurrentQueue<Fence> Fences = new ConcurrentQueue<Fence>();
	private GraphicsDevice GraphicsDevice;

	public FencePool(GraphicsDevice graphicsDevice)
	{
		GraphicsDevice = graphicsDevice;
	}

	public Fence Obtain()
	{
		if (Fences.TryDequeue(out var fence)) {
			return fence;
		}
		return new Fence();
	}

	public void Return(Fence fence)
	{
		Fences.Enqueue(fence);
	}
}