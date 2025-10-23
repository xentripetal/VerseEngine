using System.Threading.Channels;
using Serilog;

namespace Verse.Assets;

public class AssetFileWatcher
{
	private Dictionary<string, FileSystemWatcher> watchers = new ();
	private ChannelWriter<AssetSourceEvent> writer;

	public AssetFileWatcher(ChannelWriter<AssetSourceEvent> writer)
	{
		this.writer = writer;
	}

	/// <summary>
	/// Adds the provided path for monitoring <see cref="AssetSourceEvent"/>
	/// </summary>
	/// <param name="path"></param>
	/// <returns>True if the path is newly registered. False if it is already monitored by another path.</returns>
	public bool AddPath(string path)
	{
		path = NormalizePath(path);
		if (watchers.ContainsKey(path)) {
			return false;
		}
		// we could do some directory walking here and check if this is a subset or superset of existing paths.
		// But thats a lot of extra work for something that will be controlled by a developer not a user.
		var watcher = new FileSystemWatcher(path);
		watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName |
		                       NotifyFilters.LastWrite | NotifyFilters.Size;
		watcher.IncludeSubdirectories = true;
		watcher.Changed += OnChanged;
		watcher.Created += OnCreated;
		watcher.Deleted += OnDeleted;
		watcher.Renamed += OnRenamed;
		watcher.Error += OnError;
		watcher.EnableRaisingEvents = true;
		return true;
	}

	private void SendAssetEvent(AssetSourceEvent evt)
	{
		if (!writer.TryWrite(evt)) {
			Log.Warning("Failed writing asset source event {evt} to channel");
		}
	}

	private AssetSourceEventObject GetAssetType(string path)
	{
		if (path.EndsWith(".meta.xml")) {
			return AssetSourceEventObject.Meta;
		}
		if (path.EndsWith("/")) {
			return AssetSourceEventObject.Folder;
		}
		try {
			FileAttributes attr = File.GetAttributes(path);
			if ((attr & FileAttributes.Directory) != 0) {
				return AssetSourceEventObject.Folder;
			}
			return AssetSourceEventObject.Asset;
		}
		catch (Exception e) {
			return AssetSourceEventObject.Unknown;
		}
	}

	private void OnChanged(object sender, FileSystemEventArgs e)
	{
		SendAssetEvent(new AssetSourceEvent(AssetSourceEventType.Modified, GetAssetType(e.FullPath), e.FullPath));
	}

	private void OnCreated(object sender, FileSystemEventArgs e)
	{
		SendAssetEvent(new AssetSourceEvent(AssetSourceEventType.Added, GetAssetType(e.FullPath), e.FullPath));
	}

	private void OnDeleted(object sender, FileSystemEventArgs e)
	{
		SendAssetEvent(new AssetSourceEvent(AssetSourceEventType.Removed, GetAssetType(e.FullPath), e.FullPath));
	}

	private void OnRenamed(object sender, RenamedEventArgs e)
	{
		SendAssetEvent(new AssetSourceEvent(AssetSourceEventType.Renamed, GetAssetType(e.FullPath), e.FullPath, e.OldFullPath));
	}

	private void OnError(object sender, ErrorEventArgs e)
	{
		Log.Error(e.GetException(), "Error in asset file watcher");
	}

	public static string NormalizePath(string path)
	{
		return Path.GetFullPath(new Uri(path).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
	}

}