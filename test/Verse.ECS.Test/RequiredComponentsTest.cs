namespace Verse.ECS.Test;

public class RequiredComponentsTest : EcsTestBase
{
	public struct A
	{
		public int Value;
	}

	public struct B
	{

		public int Value;
	}

	public struct C
	{
		public int Value;
	}

	public struct ATag { };
	public struct BTag { };

	[Fact]
	public void SimpleRequirement()
	{
		World.RegisterRequiredComponents<A, B>();
		var entity = World.Entity();
		entity.Set(new A());
		Assert.True(entity.Has<B>());
	}
}