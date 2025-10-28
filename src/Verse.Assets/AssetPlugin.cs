using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Assets;

public class AssetPlugin : IPlugin
{
	public readonly string AssetPath;
	public void Build(App app)
	{
		app.World.InitResource<AssetSources.Builder>();
		var builder = app.World.Resource<AssetSources.Builder>();
		var source = builder.Build();
		var server = new AssetServer(source, false);
		app.World.InsertResource(server);
		var handleInternalAssetEvents = FuncSystem.Of<World>(AssetServer.HandleInternalAssetEvents);
		app.ConfigureSets(Schedules.PreUpdate, SystemSet.Of(AssetSystems.TrackAssetSystems).After(handleInternalAssetEvents));
		// `handle_internal_asset_events` requires the use of `&mut World`,
		// and as a result has ambiguous system ordering with all other systems in `PreUpdate`.
		// This is virtually never a real problem: asset loading is async and so anything that interacts directly with it
		// needs to be robust to stochastic delays anyways.
		app.AddSystems(Schedules.PreUpdate, handleInternalAssetEvents.AmbiguousWithAll());
	}
}