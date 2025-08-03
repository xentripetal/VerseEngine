using System.Collections.Concurrent;

namespace Verse.MoonWorks.Graphics;

internal class RenderPassPool
{
	private readonly ConcurrentQueue<RenderPass> RenderPasses = new ConcurrentQueue<RenderPass>();

	public RenderPass Obtain()
	{
		if (RenderPasses.TryDequeue(out var renderPass)) {
			return renderPass;
		}
		return new RenderPass();
	}

	public void Return(RenderPass renderPass)
	{
		RenderPasses.Enqueue(renderPass);
	}
}