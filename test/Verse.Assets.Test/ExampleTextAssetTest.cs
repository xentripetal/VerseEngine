using System.Runtime.InteropServices.ComTypes;
using Serilog;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;
using Xunit.Abstractions;

namespace Verse.Assets.Test;

public class AssetServerTests
{
	public AssetServerTests(ITestOutputHelper output)
	{
		Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestOutput(output).CreateLogger();
	}
	[Fact]
	public void TestAssetLoad()
	{
		var app = buildBaseApp();
		app.InitResource<ExampleResource>();
		var setup = LoadResourceAsset;
		app.AddSystems(Schedules.Startup, FuncSystem.Of(setup));
		var check = ExitOnLoaded;
		app.AddSystems(Schedules.Update, FuncSystem.Of(check));
		app.Run();
	}
	
	[Fact]
	public void TestAssetUnload()
	{
		var app = buildBaseApp();
		app.InitResource<ExampleResource>();
		var setup = LoadEntityAsset;
		app.AddSystems(Schedules.Startup, FuncSystem.Of(setup));
		var check = WaitForEntityAssetLoaded;
		app.AddSystems(Schedules.Update, FuncSystem.Of(check));
		var teardown = WaitForNoAssets;
		app.AddSystems(Schedules.PostUpdate, FuncSystem.Of(teardown));
		app.Run();
	}

	private App buildBaseApp()
	{
		var source = new MockAssetSource();
		source.AddFile("file.txt", "Hello, World!"u8.ToArray());
		var app = App.Default();
		app.SetRunner(App.Run);
		app.SetDefaultAssetSource(source);
		app.AddPlugin(new AssetPlugin());
		app.RegisterAssetLoader<TextAssetLoader, TextAsset, TextAssetLoader.Settings>(new TextAssetLoader());
		app.InitAsset<TextAsset>();
		return app;
	}

	public void LoadEntityAsset(Commands commands, Res<AssetServer> server)
	{
		var handle = server.Value.Load<TextAsset>(AssetPath.ParseUri("file.txt"));
		commands.Entity().Set(handle);
		commands.Entity().Set(handle);
		commands.Entity().Set(handle);
		Log.Information("Created entity with asset handle");
	}

	public void WaitForEntityAssetLoaded(Res<AssetServer> server, Query<Data<Handle<TextAsset>>> q, Commands commands)
	{
		foreach (var (entity, handle) in q) {
			if (server.Value.IsLoaded(handle.Ref)) {
				Log.Information("Entity loaded, deleting");
				commands.Entity(entity.Ref).Delete();
			}
		}
	}
	
	public void WaitForNoAssets(Res<Assets<TextAsset>> assets, MessageWriter<AppExit> exitWriter)
	{
		if (assets.Value.Length == 0) {
			Log.Information("No more assets loaded, exiting");
			exitWriter.Enqueue(AppExit.Success());
		}
	}


	public void LoadResourceAsset(Res<AssetServer> server, ResMut<ExampleResource> res)
	{
		var handle = server.Value.Load<TextAsset>(AssetPath.ParseUri("file.txt"));
		Assert.False(handle.Id().Invalid());
		res.Value.Asset = handle;
	}

	public void ExitOnLoaded(Res<AssetServer> server, Res<Assets<TextAsset>> assets, Res<ExampleResource> res, MessageWriter<AppExit> exitWriter)
	{
		if (server.Value.IsLoaded(res.Value.Asset)) {
			var asset = assets.Value.Get(res.Value.Asset);
			Log.Information("Got asset with Value: {asset}", asset.MyValue);
			exitWriter.Enqueue(AppExit.Success());
		}
	}
}

public class TextAsset : IAsset<TextAsset>
{
	public string MyValue;
}

public class ExampleResource : IFromWorld<ExampleResource>
{
	public Handle<TextAsset> Asset;
	public static ExampleResource FromWorld(World world) => new ();
}

public class MockAssetSource : IAssetSource
{
	public MockAssetSource()
	{
		root = new DirectoryNode("");
	}
	public void AddFile(string path, byte[] data)
	{
		var parts = path.Split("/");
		var curNode = root;
		// traverse all the folders until were at the node we want to put the file in
		for (int i = 0; i < parts.Length - 1; i++) {
			if (!curNode.Folders.TryGetValue(parts[i], out curNode)) {
				var newFolder = new DirectoryNode(parts[i]);
				curNode.Folders.Add(newFolder.Name, newFolder);
				newFolder.Parent = curNode;
				curNode = newFolder;
			}
		}

		curNode.Files[parts[^1]] = data;
	}

	/// <summary>
	/// Hacky representation of a file system
	/// </summary>
	private class DirectoryNode
	{
		public DirectoryNode(string name)
		{
			Name = name;
		}
		public string Name;
		public DirectoryNode? Parent = null;
		public Dictionary<string, DirectoryNode> Folders = new ();
		public Dictionary<string, byte[]> Files = new ();
	}

	private DirectoryNode root;

	public Task<Stream> Read(string path)
	{
		return Task.FromResult(InternalReadFile(path));
	}

	private Stream InternalReadFile(string path)
	{
		var parts = path.Split("/");
		var curNode = root;
		for (int i = 0; i < parts.Length - 1; i++) {
			if (!curNode.Folders.TryGetValue(parts[i], out curNode)) {
				throw new FileNotFoundException($"Folder not found: {parts[i]}", path);
			}
		}
		if (curNode.Files.TryGetValue(parts[^1], out var data)) {
			return new MemoryStream(data);
		}
		throw new FileNotFoundException("Mock file not found.", path);
	}
	public Task<Stream> ReadMeta(string path)
	{
		return Task.FromResult(InternalReadFile(path + ".meta.xml"));
	}
	public Task<bool> IsDirectory(string path)
	{
		var parts = path.Split("/");
		var curNode = root;
		foreach (var part in parts) {
			if (!curNode.Folders.TryGetValue(part, out curNode)) {
				return Task.FromResult(false);
			}
		}
		return Task.FromResult(true);
	}
	public Task<IEnumerable<string>> ListDirectoryContents(string path)
	{
		var parts = path.Split("/");
		var curNode = root;
		foreach (var part in parts) {
			if (!curNode.Folders.TryGetValue(part, out curNode)) {
				throw new FileNotFoundException($"Folder not found: {part}", path);
			}
		}
		return Task.FromResult<IEnumerable<string>>(curNode.Folders.Keys);
	}
}

public class TextAssetLoader : IAssetLoader<TextAsset, TextAssetLoader.Settings>
{
	public struct Settings : ISettings { }
	public async Task<UntypedLoadedAsset> Load(Stream stream, IAssetMeta assetMeta, LoadContext context)
	{
		var asset = await Load(stream, (Settings)assetMeta.LoaderSettings, context);
		return new UntypedLoadedAsset(asset);
	}
	public List<string> Extensions { get => ["txt"]; }
	public Task<TextAsset> Load(Stream stream, Settings settings, LoadContext context)
	{
		var streamContent = "";
		using (StreamReader reader = new StreamReader(stream))
		{
			streamContent = reader.ReadToEnd();
		}
		return Task.FromResult(new TextAsset{MyValue = streamContent});
	}
	public Type AssetType { get => typeof(TextAsset); }
	public IAssetMeta DeserializeMeta(Stream stream) => throw new NotImplementedException();
	public IAssetMeta DefaultMeta() => new TextAssetMeta();
}

public struct TextAssetMeta : IAssetMeta
{
	public TextAssetMeta() { }
	public ISettings? LoaderSettings { get; set; } = new TextAssetLoader.Settings();
	public ProcessedInfo? ProcessedInfo { get; set; }
}
