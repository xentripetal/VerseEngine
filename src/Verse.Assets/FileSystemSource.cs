namespace Verse.Assets;

/// <summary>
/// A <see cref="IAssetSource"/> using csharps <see cref="System.IO"/> APIs to read assets from the local filesystem.
/// </summary>
/// <remarks>
/// Generally this should not be used if you're using SDL/Moonworks which has its own source system that is abstracted
/// for different platforms.
/// </remarks>
public class FileSystemSource : IAssetSource
{
	private DirectoryInfo _root;
	public FileSystemSource(string rootPath)
	{
		_root = new DirectoryInfo(rootPath);
		if (!_root.Exists) {
			throw new DirectoryNotFoundException($"Root directory {rootPath} does not exist");
		}
	}

	private void ValidatePath(string path)
	{
		if (!path.StartsWith(_root.FullName,
			    StringComparison.InvariantCultureIgnoreCase)) {
			throw new ArgumentException($"Path {path} is outside of root directory {_root.FullName}");
		}
	}

	public string GetMetaPath(string path)
	{
		if (path.EndsWith(".meta.xml")) {
			return path;
		}
		return path + ".meta.xml";
	}

	public async Task<Stream> Read(string path)
	{
		var fullPath = Path.Combine(_root.FullName, path);
		ValidatePath(fullPath);
		return File.OpenRead(fullPath);
	}
	public async Task<Stream> ReadMeta(string path)
	{
		var fullPath = Path.Combine(_root.FullName, GetMetaPath(path));
		ValidatePath(fullPath);
		return File.OpenRead(fullPath);
	}
	public async Task<bool> IsDirectory(string path)
	{
		var fullPath = Path.Combine(_root.FullName, path);
		ValidatePath(fullPath);
		return Directory.Exists(fullPath);
	}
	public async Task<IEnumerable<string>> ListDirectoryContents(string path)
	{
		var fullPath = Path.Combine(_root.FullName, path);
		ValidatePath(fullPath);
		return Directory.EnumerateFileSystemEntries(fullPath);
	}
}