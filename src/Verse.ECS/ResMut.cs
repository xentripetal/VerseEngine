using Verse.ECS.Systems;

namespace Verse.ECS;

public class ResMut<T> : ISystemParam, IIntoSystemParam<ResMut<T>>
{
	private T? _t;

	public ref T? Value => ref _t;

	public bool HasValue => !Unsafe.IsNullRef(ref _t) && _t is not null;

	public static ResMut<T> Generate(World arg)
	{
		if (arg.Entity<Placeholder<ResMut<T>>>().Has<Placeholder<ResMut<T>>>())
			return arg.Entity<Placeholder<ResMut<T>>>().Get<Placeholder<ResMut<T>>>().Value;

		var res = new ResMut<T>();
		arg.Entity<Placeholder<ResMut<T>>>().Set(new Placeholder<ResMut<T>> { Value = res });
		return res;
	}

	public static implicit operator T?(ResMut<T> reference)
		=> reference.Value;
	public void Init(ISystem system, World world)
	{
		var c = world.GetComponent<Placeholder<ResMut<T>>>();
		system.Meta.Access.AddUnfilteredWrite(c.Id);
	}
	public bool Ready(ISystem system, World world) => true;
}