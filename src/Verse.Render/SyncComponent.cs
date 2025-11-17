using Verse.Core;

namespace Verse.Render;

public struct SyncComponentPlugin<T> : IPlugin
{
	public void Build(App app)
	{
		app.RegisterRequiredComponents<T, SyncToRenderWorld>();
		app.World.RegisterComponentHook<T>().SetOnRemove((entity => {
			entity.World.Resource<PendingSyncEntity>().Add(new EntityRecord(EntityRecord.OpType.Removed, entity));
		}));
	}
}