using System.Runtime.InteropServices;
using SDL3;
using Serilog;

namespace Verse.MoonWorks;

public static class Logger
{
	public static Action<string> LogInfo = LogInfoDefault;
	public static Action<string> LogWarn = LogWarnDefault;
	public static Action<string> LogError = LogErrorDefault;

	private static void LogInfoDefault(string str)
	{
		Log.Information(str);
	}

	private static void LogWarnDefault(string str)
	{
		Log.Warning(str);
	}

	private static void LogErrorDefault(string str)
	{
		Log.Error(str);
	}

	internal static unsafe void InitSDLLogging()
	{
		SDL.SDL_SetLogPriority((int)SDL.SDL_LogCategory.SDL_LOG_CATEGORY_GPU, SDL.SDL_LogPriority.SDL_LOG_PRIORITY_INFO);
		SDL.SDL_SetLogOutputFunction(SDLLog, IntPtr.Zero);
	}

	internal static unsafe void SDLLog(IntPtr userdata, int category, SDL.SDL_LogPriority priority, byte* message)
	{
		if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_INFO) {
			Log.Information(Marshal.PtrToStringUTF8((nint)message));
		} else if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_WARN) {
			Log.Warning(Marshal.PtrToStringUTF8((nint)message));
		} else if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_ERROR) {
			Log.Error(Marshal.PtrToStringUTF8((nint)message));
		}
	}
}