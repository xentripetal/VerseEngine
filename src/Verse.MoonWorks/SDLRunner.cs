using Verse.Core;

namespace Verse.MoonWorks;

public class SDLRunner
{
	public SDLRunner(BaseSDLApp sdlApp)
	{
		this.SDLApp = sdlApp;
	}
	
	protected BaseSDLApp SDLApp;

	public AppExit Run(App app)
	{
		if (app.PluginsState() == PluginsState.Ready) {
			app.Finish();
			app.Cleanup();
		}
		app.InitRes(SDLApp);
		return SDLApp.DirectRun();
	}
}