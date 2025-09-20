using Verse.Core;

namespace Verse.Render;

public class SpritePlugin : IPlugin
{

	public void Build(App app)
	{
		var renderApp = app.GetSubApp(RenderApp.Name);
		if (renderApp == null) {
			return;
		}
	}
}