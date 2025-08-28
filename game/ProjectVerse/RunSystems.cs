using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;
using Verse.Core;

namespace ProjectVerse;

public class Example
{
	public int Value;
}

public partial class RunSystems
{
	[Schedule]
	[Before<Sets>(Sets.Act2)]
	public void Act1(Commands commands, Query<Data<int>, Writes<int>> q)
	{
		foreach (var (e, data) in q) {
			if (data.Ref < 4) {
				data.Mut++;
			} else {
				commands.Entity().Set(1);
			}
		}
	}

	[Schedule]
	public void Act2(ResMut<Example> b)
	{
		if (!b.HasValue) {
			b.Value = new Example();
		} else {
			b.Value.Value++;
		}
	}

	[Schedule]
	[After<Sets>(Sets.Act2)]
	public void Act3(Query<Data<int>, Changed<int>> q, ResMut<Example> b)
	{
		Console.WriteLine(b.Value.Value);
		foreach (var (entity, data) in q) {
			Console.WriteLine($"Entity {entity.Ref.Id} - {data.Ref}");
		}
	}
}