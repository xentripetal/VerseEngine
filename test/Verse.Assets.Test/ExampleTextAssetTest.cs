using System.Runtime.InteropServices.ComTypes;
using Verse.Core;
using Verse.ECS;
using Verse.ECS.Systems;

namespace Verse.Assets.Test;

public class TextAsset : IAsset<TextAsset>
{
	public string MyValue;
}

public struct ExampleResource
{
	public Handle<TextAsset> Asset;
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
	public Task<bool> IsDirectory(string path) {
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
				throw  new FileNotFoundException($"Folder not found: {part}", path);
			}
		}
		return Task.FromResult<IEnumerable<string>>(curNode.Folders.Keys);
	}
}

public class AssetServerTests
{
	[Fact]
	public void Test1()
	{
		var source = new MockAssetSource();
		var app = App.Default();
		app.SetDefaultAssetSource(source);
		app.AddPlugin(new AssetPlugin());
		var fn = StartupLoadSystem;
		app.InitRes<ExampleResource>();
		app.AddSystems(Schedules.Startup, FuncSystem.Of(fn));
		app.Run();
	}

	public void StartupLoadSystem(Res<AssetServer> server, ResMut<ExampleResource> res)
	{
		var handle = server.Value.Load<TextAsset>(AssetPath.ParseUri("file.txt"));
		Assert.False(handle.Id().Invalid());
		res.Value.Asset = handle;
	}
}