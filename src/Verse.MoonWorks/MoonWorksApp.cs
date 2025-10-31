using SDL3;
using Verse.Core;
using Verse.ECS;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Storage;
using Verse.Assets;

namespace Verse.MoonWorks;

public class MoonWorksApp : BaseSDLApp
{

	protected App VerseApp;
	protected AppInfo AppInfo;
	protected SDL.SDL_InitFlags InitFlags;

	protected TitleStorage TitleStorage;
	protected UserStorage UserStorage;
	protected World World;
	public MoonWorksApp(App app, AppInfo appInfo, SDL.SDL_InitFlags initFlags)
	{
		VerseApp = app;
		AppInfo = appInfo;
		SDL.SDL_Init(InitFlags);
		Logger.InitSDLLogging();
		// We need storage before actually running Init so we can set up asset sources
		TitleStorage = new TitleStorage("Assets");
		UserStorage = new UserStorage(AppInfo);
		VerseApp.SetDefaultAssetSource(new TitleStorageAssetSource(TitleStorage));
	}

	protected override SDL.SDL_AppResult Init()
	{
		World = VerseApp.World;
		// Storage is essential for most systems, so we init it here
		// Is this safe? I think so? plugins should be Building on main thread so at this point we should be on main thread
		VerseApp.InsertResource(TitleStorage);
		VerseApp.InsertResource(UserStorage);
		return SDL.SDL_AppResult.SDL_APP_CONTINUE;
	}

	protected override SDL.SDL_AppResult Iterate()
	{
		var pluginState = VerseApp.PluginsState();
		if (pluginState == PluginsState.Ready) {
			VerseApp.Finish();
			VerseApp.Cleanup();
			pluginState = VerseApp.PluginsState();
		}

		if (pluginState == PluginsState.Cleaned) {
			VerseApp.Update();
		}
		var exit = VerseApp.ShouldExit();
		if (exit == null) return SDL.SDL_AppResult.SDL_APP_CONTINUE;
		return exit.Value.IsErr ? SDL.SDL_AppResult.SDL_APP_FAILURE : SDL.SDL_AppResult.SDL_APP_SUCCESS;
	}


	protected override unsafe SDL.SDL_AppResult OnEvent(SDL.SDL_Event* e)
	{
		// I think this should be on the main thread?  Should be safe to just push down the events into our queue.
		switch (e->type) {
			// Send this down to ECS so systems can react to it. It will get picked up during Iterate and shut down from there.
			case (uint)SDL.SDL_EventType.SDL_EVENT_QUIT:
				World.WriteMessage(AppExit.Success());
				break;
			case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
				World.WriteMessage(new CloseWindowRequest(e->window.windowID));
				break;
			default:
				// If its something we don't have an explicit case for, just push the raw event down.
				SDL.SDL_Event copy;
				copy = *e;
				World.WriteMessage(copy);
				break;
		}
		return SDL.SDL_AppResult.SDL_APP_CONTINUE;
	}
	protected override void OnQuit(SDL.SDL_AppResult result)
	{
		Logger.LogInfo("Shutting down...");
		UserStorage.Dispose();
		TitleStorage.Dispose();
		// TODO do you actually call this when using managed app?
		SDL.SDL_Quit();
	}
}