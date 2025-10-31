using SDL3;
using Verse.Assets;
using Verse.MoonWorks.Storage;

namespace Verse.MoonWorks;

public class TitleStorageAssetSource : IAssetSource
{
	public TitleStorageAssetSource(TitleStorage storage)
	{
		this.storage = storage;
	}
	private TitleStorage storage;

	public async Task<Stream> Read(string path)
	{
		return await Task.Run(() => {
				if (!storage.GetFileSize(path, out var size)) {
					throw new FileNotFoundException($"File at path {path} not found in TitleStorage.");
				}
				var buffer = size <= int.MaxValue ? new byte[(int)size] : new byte[size];
				storage.ReadFile(path, buffer);
				return Task.FromResult<Stream>(new MemoryStream(buffer, writable: false));
			}
		);
	}
	public Task<Stream> ReadMeta(string path)
	{
		return Read(path + ".meta.xml");
	}
	
	public async Task<bool> IsDirectory(string path)
	{
		return await Task.Run(() => {
			if (!storage.GetPathInfo(path, out var info)) {
				return false;
			}
			return info.type == SDL.SDL_PathType.SDL_PATHTYPE_DIRECTORY;
		});
	}
	public async Task<IEnumerable<string>> ListDirectoryContents(string path)
	{
		return await Task.Run(() => {
			var files = new List<string>();
			storage.EnumerateDirectory(path, (dir, file) => {
				files.Add(file);
				return true;
			});
			return files.AsEnumerable();
		});
	}
}