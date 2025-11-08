namespace Verse.ECS.Test;

/// <summary>
/// Base class for ECS tests that provides a World instance and handles cleanup.
/// </summary>
public abstract class EcsTestBase : IDisposable
{
	protected readonly World World;

	protected EcsTestBase()
	{
		World = new World();
	}

	public void Dispose()
	{
		World?.Dispose();
		GC.SuppressFinalize(this);
	}
}
