using Verse.Core;

namespace Verse.Assets;

public static class AppExtensions
{
	public static AssetServer AssetServer(this App app)
	{
		return app.World.MustGetRes<AssetServer>().Value;
	}

	public static App AddAssetSource(this App app, IAssetSource source, string name)
	{
		var builder = app.World.GetResMutOrDefault<AssetSources.Builder>();
		builder.Value.AddSource(source, name);
		return app;
	}

	public static App SetDefaultAssetSource(this App app, IAssetSource source)
	{
		var builder = app.World.GetResMutOrDefault<AssetSources.Builder>();
		builder.Value.ReplaceDefault(source);
		return app;
	}

	public static App InitAsset<T>(this App app)
		where T : IAsset
	{
		var assets = new Assets<T>();
		var server = app.World.MustGetResMut<AssetServer>().Value;
		// todo asset processor
		server.RegisterAsset(assets);
		app.InitRes(assets);
		app.World.AllowAmbiguousResource<Assets<T>>();
		app.AddMessage<AssetEvent<T>>();
		app.AddMessage<AssetLoadFailedEvent<T>>();
		app.AddSystems(Schedules.PostUpdate, assets.TrackAssets())
		// 	.add_message::<AssetEvent<A>>()
		// 	.add_message::<AssetLoadFailedEvent<A>>()
		// 	.register_type::<Handle<A>>()
		// 	.add_systems(
		// 		PostUpdate,
		// 		Assets::<A>::asset_events
		// 	.run_if(Assets::<A>::asset_events_condition)
		// 	.in_set(AssetEventSystems),
		// 	)
		// 	.add_systems(
		// 	PreUpdate,
		// 	Assets::<A>::track_assets.in_set(AssetTrackingSystems),
		// 	)

	}
}