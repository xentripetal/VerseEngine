using System.Collections.Concurrent;

namespace Verse.MoonWorks.Graphics;

internal class CopyPassPool
{
	private readonly ConcurrentQueue<CopyPass> CopyPasses = new ConcurrentQueue<CopyPass>();

	public CopyPass Obtain()
	{
		if (CopyPasses.TryDequeue(out var copyPass)) {
			return copyPass;
		}
		return new CopyPass();
	}

	public void Return(CopyPass copyPass)
	{
		CopyPasses.Enqueue(copyPass);
	}
}