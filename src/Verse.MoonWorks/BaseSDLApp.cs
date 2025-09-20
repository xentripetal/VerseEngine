using SDL3;
using Serilog;
using System.Runtime.InteropServices;
using Verse.Core;

namespace Verse.MoonWorks;

public abstract class BaseSDLApp
{
	public unsafe AppExit Run(string[] args)
	{
		var argv = TranslateArgs(args, out var argc);
		try {
			var exitCode = SDL.SDL_EnterAppMainCallbacks(argc, argv, SDL_AppInit, SDL_AppIterate, SDL_AppEvent, SDL_AppQuit);
			if (exitCode == 0) {
				return AppExit.Success();
			}
			return AppExit.Err(exitCode);
		}
		finally {
			// TODO actually test this.
			// Clean up allocated memory
			for (int i = 0; i < argc; i++) {
				IntPtr strPtr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
				Marshal.FreeHGlobal(strPtr);
			}
		}
	}

	public AppExit DirectRun()
	{
		var state = SDL_AppInit(IntPtr.Zero, 0, IntPtr.Zero);
		if (state != SDL.SDL_AppResult.SDL_APP_CONTINUE) {
			return MapAppExit(state);
		}
		while (state == SDL.SDL_AppResult.SDL_APP_CONTINUE) {
			while (SDL.SDL_PollEvent(out var _event)) {
				unsafe {
					state = SDL_AppEvent(IntPtr.Zero, &_event);
				}
				if (state != SDL.SDL_AppResult.SDL_APP_CONTINUE) {
					goto quit;
				}
			}
			state = SDL_AppIterate(IntPtr.Zero);
		}
	quit:
		SDL_AppQuit(IntPtr.Zero, state);
		return MapAppExit(state);
	}

	protected AppExit MapAppExit(SDL.SDL_AppResult result)
	{
		if (result == SDL.SDL_AppResult.SDL_APP_SUCCESS) {
			return AppExit.Success();
		}
		return AppExit.Err((int)result);
	}

	private IntPtr TranslateArgs(string[] args, out int argc)
	{
		argc = args.Length;
		IntPtr argv = Marshal.AllocHGlobal(IntPtr.Size * argc);
		for (int i = 0; i < argc; i++) {
			IntPtr strPtr = Marshal.StringToHGlobalAnsi(args[i]);
			Marshal.WriteIntPtr(argv, i * IntPtr.Size, strPtr);
		}
		return argv;
	}

	SDL.SDL_AppResult SDL_AppInit(IntPtr appstate, int argc, IntPtr argv)
	{
		try {
			return Init();
		}
		catch (Exception e) {
			Log.Error(e, "Exception in SDL_AppInit. Shutting down.");
			return SDL.SDL_AppResult.SDL_APP_FAILURE;
		}
	}

	protected abstract SDL.SDL_AppResult Init();

	SDL.SDL_AppResult SDL_AppIterate(IntPtr appstate)
	{
		try {
			return Iterate();
		}
		catch (Exception e) {
			Log.Error(e, "Exception in SDL_AppIterate. Shutting down.");
			return SDL.SDL_AppResult.SDL_APP_FAILURE;
		}
	}

	protected abstract SDL.SDL_AppResult Iterate();

	unsafe SDL.SDL_AppResult SDL_AppEvent(IntPtr appstate, SDL.SDL_Event* e)
	{
		try {
			return OnEvent(e);
		}
		catch (Exception ex) {
			Log.Error(ex, "Exception in SDL_AppEvent. Shutting down.");
			return SDL.SDL_AppResult.SDL_APP_FAILURE;
		}
	}

	protected abstract unsafe SDL.SDL_AppResult OnEvent(SDL.SDL_Event* e);

	void SDL_AppQuit(IntPtr appstate, SDL.SDL_AppResult result)
	{
		try {
			OnQuit(result);
		}
		catch (Exception e) {
			Log.Error(e, "Exception in SDL_AppQuit.");
		}
	}

	protected abstract void OnQuit(SDL.SDL_AppResult result);
}