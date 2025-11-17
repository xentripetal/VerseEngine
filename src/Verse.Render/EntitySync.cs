using Verse.Core;
using Verse.ECS;

namespace Verse.Render;

/// <summary>
/// Marker component that indicates that its entity needs to be synchronized to the render world.
/// </summary>
/// TODO docs on required components and ExtractComponentPlugin and SyncComponentPlugin
public record struct SyncToRenderWorld : IHookedComponent<SyncToRenderWorld>
{
	public void OnAdd(EntityView view)
	{
		view.World.Resource<PendingSyncEntity>().Add(new EntityRecord(EntityRecord.OpType.Added, view.Id));
	}
	public void OnSet(EntityView view)
	{
	}
	public void OnRemove(EntityView view)
	{
		if (view.Has<RenderEntity>()) {
			view.World.Resource<PendingSyncEntity>().Add(new  EntityRecord(EntityRecord.OpType.Removed, view.Get<RenderEntity>().EntityId));
		}
	}
}

/// <summary>
/// Tag component that indicats thats entity needs to be despawned at the end of the frame.
/// </summary>
public record struct TemporaryRenderEntity { }

/// <summary>
/// Component added on the main world entities that are synced to the Render world in order to keep
/// track of the corresponding render world entity.
/// </summary>
/// <param name="EntityId"></param>
public record struct RenderEntity(ulong EntityId) { }

/// <summary>
/// Component added on the render world entities to keep track of the corresponding main world entity.
/// </summary>
/// <seealso cref="RenderEntity"/>
/// <param name="EntityId"></param>
public record struct MainEntity(ulong EntityId) { }

public record struct EntityRecord(EntityRecord.OpType Op, ulong EntityId)
{
	public enum OpType
	{
		/// <summary>
		/// When an entity is spawned on the main world, notify the render world so that it can spawn a corresponding entity.
		/// Note that the entityId will belong to the main world.
		/// </summary>
		Added,
		/// <summary>
		/// When an entity is despawned on the main world, notify the render world so that it can despawn a corresponding entity.
		/// Note that the entityId will belong to the render world.
		/// </summary>
		Removed,
		/// <summary>
		/// When an entity is spawned on the main world, notify the render world so that the corresponding component can be removed
		/// Note that the entityId will belong to the main world.
		/// </summary>
		ComponentRemoved
	}
}

public class PendingSyncEntity
{
	public Queue<EntityRecord> Records = new Queue<EntityRecord>();
	public void Add(EntityRecord record) => Records.Enqueue(record);
}

/// <summary>
/// The simulation world of the application, stored as a resource.
///
/// This resource is only available during <see cref="RenderSchedules.Extract"/> 
/// </summary>
/// <param name="world"></param>
public record struct MainWorld(World world) { }

public partial class EntitySyncSystems
{
	public static void EntitySync(World mainWorld, World renderWorld)
	{
		var pendingRes = mainWorld.Resource<PendingSyncEntity>();
		var records = pendingRes.Records;
		EntityView mainEntity = new EntityView();

		while (records.TryDequeue(out var record)) {
			switch (record.Op) {
				case EntityRecord.OpType.Added:
					mainEntity = mainWorld.Entity(record.EntityId);
					if (mainEntity.Has<RenderEntity>()) {
						throw new InvalidOperationException("Entity already has a RenderEntity");
					}
					var renderEntity = renderWorld.Entity();
					renderEntity.Set(new MainEntity(record.EntityId));
					mainEntity.Set(new RenderEntity(renderEntity.Id));
					break;
				case EntityRecord.OpType.Removed:
					if (!renderWorld.Exists(record.EntityId)) {
						renderWorld.Delete(record.EntityId);
					}
					break;
				case EntityRecord.OpType.ComponentRemoved:
					mainEntity = mainWorld.Entity(record.EntityId);
					if (!mainEntity.Has<RenderEntity>()) continue;
					var renderEntityComponent = mainEntity.Get<RenderEntity>();
					if (!renderWorld.Exists(renderEntityComponent.EntityId)) continue;

					// we delete the full matching entity and setup an empty one, extract will populate it.
					renderWorld.Delete(renderEntityComponent.EntityId);
					var newRenderEntity = renderWorld.Entity();
					newRenderEntity.Set(new MainEntity(record.EntityId));
					mainEntity.Set(new RenderEntity(newRenderEntity.Id));
					break;
			}
		}
	}


	[ExclusiveSystem]
	[Schedule(RenderSchedules.Render)]
	[InSet<RenderSets>(RenderSets.PostCleanup)]
	public static void DespawnTemporaryRenderEntities(World world, Query<Empty, With<TemporaryRenderEntity>> q)
	{
		foreach (var (chunk, _)in q) {
			foreach (var entity in chunk) {
				world.Delete(entity);
			}
		}
	}
}