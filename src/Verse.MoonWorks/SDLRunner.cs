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
		app.InsertResource(SDLApp);
		return SDLApp.Run([]);
	}
}