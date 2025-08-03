using System.Runtime.InteropServices;

namespace Verse.MoonWorks.Audio;

public abstract class AudioResource : IDisposable
{

	private GCHandle SelfReference;

	protected AudioResource(AudioDevice device)
	{
		Device = device;

		SelfReference = GCHandle.Alloc(this, GCHandleType.Weak);
		Device.AddResourceReference(SelfReference);
	}
	public AudioDevice Device { get; }

	public bool IsDisposed { get; private set; }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!IsDisposed) {
			if (disposing) {
				Device.RemoveResourceReference(SelfReference);
				SelfReference.Free();
			}

			IsDisposed = true;
		}
	}

	~AudioResource()
	{
#if DEBUG
		// If you see this log message, you leaked an audio resource without disposing it!
		// We can't clean it up for you because this can cause catastrophic issues.
		// You should really fix this when it happens.
		Logger.LogWarn($"A resource of type {GetType().Name} was not Disposed.");
#endif
	}
}