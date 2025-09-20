namespace Verse.Render;

public static class RenderSchedules
{
	/// <summary>
	/// The startup schedule for <see cref="RenderApp"/>
	/// </summary>
	public const string Startup = "RenderStartup";
	/// <summary>
	/// Schedule which extract data from the main world and inserts it into the render world.
	///
	/// This step should be kept as short as possible to increase the "pipelining potential" for
	/// running the next frame while rendering the current frame.
	///
	/// This schedule is run on the main world, but its buffers are not applied
	/// until it is returned to the render world.	/// </summary>
	public const string Extract = "RenderExtract";
	/// <summary>
	/// The main render schedule
	/// </summary>
	public const string Render = "Render";
}

/// <summary>
/// The system sets of the default <see cref="RenderApp"/> rendering schedule
/// </summary>
/// <remarks>
/// These can be useful for ordering, but you almost never want to add your systems to these sets.
/// </remarks>
public enum RenderSets
{
	/// This is used for applying the commands from the [`ExtractSchedule`]
	ExtractCommands,
	/// Prepare assets that have been created/modified/removed this frame.
	PrepareAssets,
	/// Prepares extracted meshes.
	PrepareMeshes,
	/// Create any additional views such as those used for shadow mapping.
	ManageViews,
	/// Queue drawable entities as phase items in render phases ready for
	/// sorting (if necessary)
	Queue,
	/// A sub-set within [`Queue`](RenderSystems::Queue) where mesh entity queue systems are executed. Ensures `prepare_assets::<RenderMesh>` is completed.
	QueueMeshes,
	/// A sub-set within [`Queue`](RenderSystems::Queue) where meshes that have
	/// become invisible or changed phases are removed from the bins.
	QueueSweep,
	// TODO: This could probably be moved in favor of a system ordering
	// abstraction in `Render` or `Queue`
	/// Sort the [`SortedRenderPhase`](render_phase::SortedRenderPhase)s and
	/// [`BinKey`](render_phase::BinnedPhaseItem::BinKey)s here.
	PhaseSort,
	/// Prepare render resources from extracted data for the GPU based on their sorted order.
	/// Create [`BindGroups`](render_resource::BindGroup) that depend on those data.
	Prepare,
	/// A sub-set within [`Prepare`](RenderSystems::Prepare) for initializing buffers, textures and uniforms for use in bind groups.
	PrepareResources,
	/// Collect phase buffers after
	/// [`PrepareResources`](RenderSystems::PrepareResources) has run.
	PrepareResourcesCollectPhaseBuffers,
	/// Flush buffers after [`PrepareResources`](RenderSystems::PrepareResources), but before [`PrepareBindGroups`](RenderSystems::PrepareBindGroups).
	PrepareResourcesFlush,
	/// A sub-set within [`Prepare`](RenderSystems::Prepare) for constructing bind groups, or other data that relies on render resources prepared in [`PrepareResources`](RenderSystems::PrepareResources).
	PrepareBindGroups,
	/// Actual rendering happens here.
	/// In most cases, only the render backend should insert resources here.
	Render,
	/// Cleanup render resources here.
	Cleanup,
	/// Final cleanup occurs: all entities will be despawned.
	///
	/// Runs after [`Cleanup`](RenderSystems::Cleanup).
	PostCleanup,
}