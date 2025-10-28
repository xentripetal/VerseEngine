using Verse.Core;
using Verse.ECS;

namespace Verse.Assets;

public static class AppExtensions
{
	public static AssetServer AssetServer(this App app)
	{
		return app.World.Resource<AssetServer>();
	}

	public static App AddAssetSource(this App app, IAssetSource source, string name)
	{
		var builder = app.World.GetResourceOrDefault<AssetSources.Builder>();
		builder.AddSource(source, name);
		return app;
	}

	public static App SetDefaultAssetSource(this App app, IAssetSource source)
	{
		var builder = app.World.GetResourceOrDefault<AssetSources.Builder>();
		builder.ReplaceDefault(source);
		return app;
	}
	
	public static App InitAssetLoader<TLoader, TAsset, TSettings>(this App app) 
		where TLoader : IAssetLoader<TAsset, TSettings>, IFromWorld<TLoader>
		where TSettings : ISettings 
		where TAsset : IAsset
	{
		var server = app.World.Resource<AssetServer>();
		server.RegisterLoader<TLoader, TAsset, TSettings>(TLoader.FromWorld(app.World));
		return app;
	}
	
	public static App RegisterAssetLoader<TLoader, TAsset, TSettings>(this App app, TLoader loader) 
		where TLoader : IAssetLoader<TAsset, TSettings>
		where TSettings : ISettings 
		where TAsset : IAsset
	{
		var server = app.World.Resource<AssetServer>();
		server.RegisterLoader<TLoader, TAsset, TSettings>(loader);
		return app;
	}	

	public static App InitAsset<T>(this App app)
		where T : IAsset
	{
		var assets = new Assets<T>();
		var server = app.World.Resource<AssetServer>();
		// todo asset processor
		server.RegisterAsset(assets);
		app.InsertResource(assets);
		app.World.AllowAmbiguousResource<Assets<T>>();
		app.AddMessage<AssetEvent<T>>();
		app.AddMessage<AssetLoadFailedEvent<T>>();
		app.AddSchedulable(assets);
		return app;
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