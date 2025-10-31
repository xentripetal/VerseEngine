using SDL3;

namespace Verse.MoonWorks.Storage;

/// <summary>
///     Read-only abstraction over platform file storage.
///     Use this instead of System.IO for maximum portability.
/// </summary>
public class TitleStorage : IDisposable
{

	private bool IsDisposed;

	/// <summary>
	///     Opens a read-only container for the application's filesystem.
	///     Note that RootTitleStorage is provided by the Game class - you don't have to create one.
	///     If you do create a TitleStorage, make sure to Dispose it when you don't need it anymore.
	/// </summary>
	/// <param name="overrideRoot">A path to override the default root. Null will use the default root.</param>
	/// <param name="propertiesID">An optional property list that may contain backend-specific information.</param>
	public TitleStorage(string overrideRoot = null, uint propertiesID = 0)
	{
		Open(overrideRoot, propertiesID);
	}
	public IntPtr Handle { get; private set; }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Check if a file exists or not.
	/// </summary>
	/// <param name="path">A path relative to the title root.</param>
	/// <returns>True if the file exists, false otherwise.</returns>
	public bool Exists(string path) =>
		// FIXME: is it possible to pass null to the out var here?
		SDL.SDL_GetStoragePathInfo(Handle, path, out _);

	/// <summary>
	///     Query the size of a file within a storage container.
	/// </summary>
	/// <param name="path">A path relative to the title root.</param>
	/// <param name="size">Filled in with the size of the file.</param>
	/// <returns>True if the query succeeded, false otherwise.</returns>
	public bool GetFileSize(string path, out ulong size)
	{
		if (!SDL.SDL_GetStorageFileSize(Handle, path, out size)) {
			return false;
		}

		return true;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="path">A path relative to the title root.</param>
	/// 
	/// <returns>True if the query succeeded, false otherwise.</returns>
	public bool GetPathInfo(string path, out SDL.SDL_PathInfo info)
	{
		if (!SDL.SDL_GetStoragePathInfo(Handle, path, out info)) {
			Logger.LogError($"Path info query failed for path {path}: " + SDL.SDL_GetError());
			return false;
		}
		return true;
	}

	/// <summary>
	///     Synchronously read a file into a client-provided Span.
	///     The span must be the same length as the file size.
	/// </summary>
	/// <param name="path">The relative path from the title root.</param>
	/// <returns>True on success, false on failure.</returns>
	public unsafe bool ReadFile(string path, Span<byte> span)
	{
		fixed (byte* ptr = span) {
			if (!SDL.SDL_ReadStorageFile(Handle, path, (nint)ptr, (ulong)span.Length)) {
				Logger.LogError($"File at {path} failed to load: {SDL.SDL_GetError()}");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///     Enumerate the contents of a directory within the storage container.
	/// </summary>
	/// <param name="path">A path relative to the title root.</param>
	/// <param name="callback">A function that will be called for each entry. Parameters are (dirname, filename). Return true to continue enumeration, false to stop.</param>
	/// <returns>True if enumeration completed successfully, false on error.</returns>
	public unsafe bool EnumerateDirectory(string path, Func<string, string, bool> callback)
	{
		// Create a delegate that matches SDL's expected signature
		SDL.SDL_EnumerateDirectoryCallback sdlCallback = (userdata, dirname, fname) =>
		{
			var dirnameStr = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)dirname);
			var fnameStr = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)fname);

			bool shouldContinue = callback(dirnameStr ?? "", fnameStr ?? "");
			return shouldContinue
				? SDL.SDL_EnumerationResult.SDL_ENUM_CONTINUE
				: SDL.SDL_EnumerationResult.SDL_ENUM_SUCCESS;
		};

		if (!SDL.SDL_EnumerateStorageDirectory(Handle, path, sdlCallback, IntPtr.Zero)) {
			Logger.LogError($"Failed to enumerate directory at {path}: {SDL.SDL_GetError()}");
			return false;
		}

		return true;
	}

	/// <summary>
	///     Opens up a read-only container for the application's filesystem.
	/// </summary>
	/// <param name="overrideRoot">A path to override the default root. Null will use the default root.</param>
	/// <param name="propertiesID">An optional property list that may contain backend-specific information.</param>
	/// <returns></returns>
	private bool Open(string overrideRoot, uint propertiesID = 0)
	{
		if (Handle != IntPtr.Zero) {
			Logger.LogError("Storage already open! Close it first!");
			return false;
		}

		var handle = SDL.SDL_OpenTitleStorage(overrideRoot, propertiesID);
		if (handle == IntPtr.Zero) {
			Logger.LogError(SDL.SDL_GetError());
			return false;
		}

		Handle = handle;

		// Wait for the title storage to actually be ready
		while (!SDL.SDL_StorageReady(Handle)) {
			SDL.SDL_Delay(1);
		}

		return true;
	}

	/// <summary>
	///     Closes the storage container.
	/// </summary>
	private void Close()
	{
		if (!SDL.SDL_CloseStorage(Handle)) {
			Logger.LogError(SDL.SDL_GetError());
		}

		Handle = IntPtr.Zero;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!IsDisposed) {
			if (disposing) {
				// dispose managed state
			}

			if (Handle != IntPtr.Zero) {
				Close();
			}

			IsDisposed = true;
		}
	}

	~TitleStorage()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
#if DEBUG
		Logger.LogWarn("TitleStorage was not Disposed!");
#endif

		Dispose(disposing: false);
	}
}