using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;

namespace ProjectVerse;

public class Example
{
	public int Value;
}

public partial class RunSystems : SystemsContainer
{
	[Schedule]
	[After<Sets>(Sets.Act3)]
	public void Act1(Commands commands, Query<Data<int>, Writes<int>> q)
	{
		foreach (var (_, data) in q) {
			if (data.Ref < 4) {
				data.Mut++;
			} else {
				commands.Entity().Set(1);
			}
		}
	}

	[Schedule]
	public void Act2(Res<Example> b)
	{
		if (b.Value == null) {
			b.Value = new Example();
		} else {
			b.Value.Value++;
		}
	}

	[Schedule]
	[After<Sets>(Sets.Act2)]
	public void Act3(Query<Data<int>, Changed<int>> q, Res<Example> b)
	{
		Console.WriteLine(b.Value.Value);
		foreach (var (entity, data) in q) {
			Console.WriteLine($"Entity {entity.Ref.Id} - {data.Ref}");
		}
	}
}