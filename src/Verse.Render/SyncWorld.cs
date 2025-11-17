using Verse.Core;

namespace Verse.Render;

public class SyncWorldPlugin : IPlugin
{
	public void Build(App app)
	{
		app.InitResource<PendingSyncEntity>();
		// bevy registers observers for SyncToRenderWorld here, but we don't have observers yet. so just using hooks
		
	}
}