using Microsoft.CodeAnalysis.CSharp.Syntax;
using Verse.Core;
using Verse.ECS;

namespace Verse.Assets;

public static class SubAppExtensions
{
	public static AssetServer AssetServer(this SubApp app) => app.World.Resource<AssetServer>();
	public static SubApp AddAssetSource(this SubApp app, IAssetSource source, string name)
	{
		var builder = app.World.GetResourceOrDefault<AssetSources.Builder>();
		builder.AddSource(source, name);
		return app;
	}


	public static SubApp SetDefaultAssetSource(this SubApp app, IAssetSource source)
	{
		var builder = app.World.GetResourceOrDefault<AssetSources.Builder>();
		builder.ReplaceDefault(source);
		return app;
	}

	public static SubApp InitAssetLoader<TLoader, TAsset, TSettings>(this SubApp app)
		where TLoader : IAssetLoader<TAsset, TSettings>, IFromWorld<TLoader>
		where TSettings : ISettings, new()
		where TAsset : IAsset
	{
		app.InitAsset<TAsset>();
		var server = app.World.Resource<AssetServer>();
		server.RegisterLoader<TLoader, TAsset, TSettings>(TLoader.FromWorld(app.World));
		return app;
	}

	public static SubApp RegisterAssetLoader<TLoader, TAsset, TSettings>(this SubApp app, TLoader loader)
		where TLoader : IAssetLoader<TAsset, TSettings>
		where TSettings : ISettings, new()
		where TAsset : IAsset
	{
		var server = app.World.Resource<AssetServer>();
		server.RegisterLoader<TLoader, TAsset, TSettings>(loader);
		return app;
	}

	public static SubApp InitAsset<T>(this SubApp app)
		where T : IAsset
	{
		if (app.World.HasResource<Assets<T>>()) {
			return app;
		}
		var assets = new Assets<T>();
		var server = app.World.Resource<AssetServer>();
		// todo asset processor
		server.RegisterAsset(assets);
		app.InsertResource(assets);
		app.World.AllowAmbiguousResource<Assets<T>>();
		app.AddMessage<AssetEvent<T>>().
			AddMessage<AssetLoadFailedEvent<T>>().
			AddSchedulable(assets);
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

public static class AppExtensions
{
	public static AssetServer AssetServer(this App app)
	{
		return app.SubApps.Main.AssetServer();
	}

	public static App AddAssetSource(this App app, IAssetSource source, string name)
	{
		app.SubApps.Main.AddAssetSource(source, name);
		return app;
	}

	public static App SetDefaultAssetSource(this App app, IAssetSource source)
	{
		app.SubApps.Main.SetDefaultAssetSource(source);
		return app;
	}

	public static App InitAssetLoader<TLoader, TAsset, TSettings>(this App app)
		where TLoader : IAssetLoader<TAsset, TSettings>, IFromWorld<TLoader>
		where TSettings : ISettings, new()
		where TAsset : IAsset
	{
		app.SubApps.Main.InitAssetLoader<TLoader, TAsset, TSettings>();
		return app;
	}

	public static App RegisterAssetLoader<TLoader, TAsset, TSettings>(this App app, TLoader loader)
		where TLoader : IAssetLoader<TAsset, TSettings>
		where TSettings : ISettings, new()
		where TAsset : IAsset
	{
		app.SubApps.Main.RegisterAssetLoader<TLoader, TAsset, TSettings>(loader);
		return app;
	}

	public static App InitAsset<T>(this App app)
		where T : IAsset
	{
		app.SubApps.Main.InitAsset<T>();
		return app;
	}
}