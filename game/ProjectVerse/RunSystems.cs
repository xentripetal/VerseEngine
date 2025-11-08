using OneOf.Types;
using Serilog;
using Verse.Assets;
using Verse.ECS;
using Verse.ECS.Scheduling.Configs;
using Verse.Core;
using Verse.MoonWorks.Assets;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Assets;

namespace ProjectVerse;

public class Example 
{
	public DateTime LastTime;
	public DateTime ThisTime;
	public void Update()
	{
		LastTime = ThisTime;
		ThisTime = DateTime.Now;
	}
	
	public TimeSpan DeltaTime => ThisTime - LastTime;
}

public class MyAssets
{
	public Handle<Image> ExampleTexture;
}

public partial class RunSystems
{
	[Schedule(Schedules.Startup)]
	public void LoadSprite(MyAssets myAssets, AssetServer server)
	{
		myAssets.ExampleTexture = server.Load<Image>("example.png");
	}
	
	private bool wasLoaded = false;
	[Schedule]
	public void NotifyOnLoaded(MyAssets myAssets, Assets<Image> textures, AssetServer server)
	{
		if (!wasLoaded && server.IsLoaded(myAssets.ExampleTexture)) {
			wasLoaded = true;
			var texture = textures.Get(myAssets.ExampleTexture);
		}
	}
	
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
	public void Act2(Example b)
	{
		b.Update();
	}

	[Schedule]
	[After<Sets>(Sets.Act2)]
	public void Act3(Query<Data<int>, Changed<int>> q, Example b)
	{
		foreach (var (entity, data) in q) {
			Console.WriteLine($"Entity {entity.Ref} has value {data.Ref} at time {b.ThisTime}, delta {b.DeltaTime.TotalMilliseconds} ms");
		}
	}
}