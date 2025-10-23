using Verse.Core;
using Verse.ECS;

namespace Verse.Assets;

public class AssetPlugin : IPlugin
{
	public readonly string AssetPath;
	public void Build(App app)
	{
		var builder = app.World.GetResOrDefault<AssetSources.Builder>();
		var source = builder.Value.Build();
		var server = new AssetServer(source, false);
		app.World.SetRes(server);
	}

	[Schedule]
	public static void HandleInternalAssetEvents(World world)
	{
		
	}
}