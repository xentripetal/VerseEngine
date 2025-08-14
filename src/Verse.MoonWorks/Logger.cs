using System.Runtime.InteropServices;
using SDL3;

namespace Verse.MoonWorks;

public static class Logger
{
	public static Action<string> LogInfo = LogInfoDefault;
	public static Action<string> LogWarn = LogWarnDefault;
	public static Action<string> LogError = LogErrorDefault;

	private static void LogInfoDefault(string str)
	{
		Console.ForegroundColor = ConsoleColor.Green;
		Console.Write("INFO: ");
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine(str);
	}

	private static void LogWarnDefault(string str)
	{
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Write("WARN: ");
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine(str);
	}

	private static void LogErrorDefault(string str)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.Write("ERROR: ");
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine(str);
	}

	internal static unsafe void InitSDLLogging()
	{
		SDL.SDL_SetLogPriority((int)SDL.SDL_LogCategory.SDL_LOG_CATEGORY_GPU, SDL.SDL_LogPriority.SDL_LOG_PRIORITY_INFO);
		SDL.SDL_SetLogOutputFunction(SDLLog, IntPtr.Zero);
	}

	internal static unsafe void SDLLog(IntPtr userdata, int category, SDL.SDL_LogPriority priority, byte* message)
	{
		if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_INFO) {
			LogInfo(Marshal.PtrToStringUTF8((nint)message));
		} else if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_WARN) {
			LogWarn(Marshal.PtrToStringUTF8((nint)message));
		} else if (priority == SDL.SDL_LogPriority.SDL_LOG_PRIORITY_ERROR) {
			LogError(Marshal.PtrToStringUTF8((nint)message));
		}
	}
}