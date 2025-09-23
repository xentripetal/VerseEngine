using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentResults;
using Verse.ECS;
using Verse.Render.View;
using Verse.Core;
using Verse.Core.Datastructures;
using Verse.ECS.Scheduling;
using Verse.MoonWorks.Graphics;
using Verse.Render;
using Verse.Render.Batching;

namespace Verse.Render.Graph.RenderPhase;

/// <summary>
/// Labels used to uniquely identify types of material shaders
/// </summary>
public interface IShaderLabel : ILabel { }

public class ShaderLabelEnum(Enum value) : LabelEnum(value), IShaderLabel
{
	public static implicit operator ShaderLabelEnum(Enum value) => new (value);
	public static IShaderLabel Of(Enum value) => new ShaderLabelEnum(value);
}

/// <summary>
/// Stores the rendering instructions for a single phase that uses bins in all views.
/// </summary>
/// <remarks>
/// They're cleared out every frame, but storing them in a resource like this allows
/// us to reuse allocations.
///
/// Based on Bevy's ViewBinnedRenderPhases.
/// </remarks>
/// <typeparam name="TBinnedPhaseItem">The type of binned phase item.</typeparam>
public sealed class
	ViewBinnedRenderPhases<TBinnedPhaseItem, TBinKey, TBatchSetKey> : Dictionary<RetainedViewEntity, BinnedRenderPhase<TBinnedPhaseItem, TBinKey, TBatchSetKey>>
	where TBinnedPhaseItem : IBinnedPhaseItem<TBinKey, TBatchSetKey, TBinnedPhaseItem>
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey
{
	/// <summary>
	/// Prepares the render phase for a new frame.
	/// </summary>
	public void PrepareForNewFrame(RetainedViewEntity retainedViewEntity)
	{
		if (TryGetValue(retainedViewEntity, out var existingPhase)) {
			existingPhase.PrepareForNewFrame();
		} else {
			this[retainedViewEntity] = new BinnedRenderPhase<TBinnedPhaseItem, TBinKey, TBatchSetKey>();
		}
	}
}

/// <summary>
/// A collection of all rendering instructions that will be executed by the GPU, for a
/// single render phase for a single view.
/// </summary>
/// <remarks>
/// <para>
/// Each view (camera, or shadow-casting light, etc.) can have one or multiple render phases.
/// They are used to queue entities for rendering. Multiple phases might be required due to
/// different sorting/batching behaviors (e.g. opaque: front to back, transparent: back to front)
/// or because one phase depends on the rendered texture of the previous phase (e.g. for
/// screen-space reflections). All PhaseItems are then rendered using a single TrackedRenderPass.
/// The render pass might be reused for multiple phases to reduce GPU overhead.
/// </para>
/// <para>
/// This flavor of render phase is used for phases in which the ordering is less critical:
/// for example, Opaque3d. It's generally faster than the alternative SortedRenderPhase.
/// </para>
/// <para>
/// Based on Bevy's BinnedRenderPhase.
/// </para>
/// </remarks>
public sealed class BinnedRenderPhase<TBinnedPhaseItem, TBinKey, TBatchSetKey>
	where TBinnedPhaseItem : IBinnedPhaseItem<TBinKey, TBatchSetKey, TBinnedPhaseItem>
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey
{
	/// <summary>
	/// The multidrawable bins.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each batch set key maps to a batch set, which in this case is a set of meshes that can be
	/// drawn together in one multidraw call. Each batch set is subdivided into bins, each of which
	/// represents a particular mesh. Each bin contains the entity IDs of instances of that mesh.
	/// </para>
	/// <para>
	/// So, for example, if there are two cubes and a sphere present in the scene, we would generally
	/// have one batch set containing two bins, assuming that the cubes and sphere meshes are allocated
	/// together and use the same pipeline. The first bin, corresponding to the cubes, will have two
	/// entities in it. The second bin, corresponding to the sphere, will have one entity in it.
	/// </para>
	/// </remarks>
	public readonly IndexDictionary<TBatchSetKey, IndexDictionary<TBinKey, RenderBin>> MultidrawableMeshes = new ();

	/// <summary>
	/// The bins corresponding to batchable items that aren't multidrawable.
	/// </summary>
	/// <remarks>
	/// For multidrawable entities, use MultidrawableMeshes; for unbatchable entities, use UnbatchableMeshes.
	/// </remarks>
	public readonly IndexDictionary<(TBatchSetKey BatchSetKey, TBinKey BinKey), RenderBin> BatchableMeshes = new ();

	/// <summary>
	/// The unbatchable bins.
	/// </summary>
	/// <remarks>
	/// Each entity here is rendered in a separate drawcall.
	/// </remarks>
	public readonly IndexDictionary<(TBatchSetKey BatchSetKey, TBinKey BinKey), UnbatchableBinnedEntities> UnbatchableMeshes = new ();

	/// <summary>
	/// Items in the bin that aren't meshes at all.
	/// </summary>
	/// <remarks>
	/// Verse itself doesn't place anything in this list, but plugins or your app can in order to
	/// execute custom drawing commands. Draw functions for each entity are simply called in order
	/// at rendering time.
	/// </remarks>
	public readonly IndexDictionary<(TBatchSetKey BatchSetKey, TBinKey BinKey), NonMeshEntities> NonMeshItems = new ();

	/// <summary>
	/// Information on each batch set
	/// </summary>
	/// <remarks>
	/// <para> a Batch Set is a set of entities that will be batched together</para>
	/// <para> Bevy supports multiple types of batches for targets like WebGL. I think SDL3 abstracts a lot of this away
	/// so I skipped porting this and went with the assumption batching API will always be supported. </para>
	/// </remarks>
	internal List<BinnedRenderPhaseBatchSet<TBinKey>> BatchSets;

	/// <summary>
	/// The batch and bin key for each entity.
	/// </summary>
	/// <remarks>
	/// We retain these so that when the entity changes, <see cref="SweepOldEntities"/> can quickly find the bin it was
	/// located in a nd remove it.
	/// </remarks>
	private IndexDictionary<MainEntity, CachedBinnedEntity<TBinKey, TBatchSetKey>> cachedEntityBinKeys;


	/// <summary>
	/// The set of indices in <see cref="cachedEntityBinKeys"/> that are confirmed to be up to date.
	/// </summary>
	/// <remarks>Note that each bit in this bit set refers to an index in the <see cref="IndexDictionary{TKey,TValue}"/>. They aren't entity IDs</remarks>
	private FixedBitSet validCachedEntityBinKeys;

	/// <summary>
	/// The set of entities that changed bins this frame
	/// </summary>
	/// <remarks>
	/// An entity will only be present in this list if it was in one bin on the
	/// previous frame and is in a new bin on this frame. Each list entry
	/// specifies the bin the entity used to be in. We use this in order to
	/// remove the entity from the old bin during
	/// </remarks>
	private List<EntityThatChangedBins<TBinKey, TBatchSetKey>> entitiesThatChangedBins;


	/// <summary>
	/// Bins a new entity.
	/// </summary>
	/// <remarks>
	/// The <paramref name="phaseType"/> parameter specifies wheter the entity is a preprocessable mesh and
	/// whether it can be binned with meshes of the same type
	/// </remarks>
	public void Add(
		TBatchSetKey batchSetKey,
		TBinKey binKey,
		(ulong Entity, MainEntity MainEntity) entity,
		InputUniformIndex inputUniformIndex,
		BinnedRenderPhaseType phaseType,
		uint changeTick
	)
	{
		switch (phaseType) {
			case BinnedRenderPhaseType.MultidrawableMesh:
				if (!MultidrawableMeshes.TryGetValue(batchSetKey, out var batchSet)) {
					batchSet = new IndexDictionary<TBinKey, RenderBin>();
					MultidrawableMeshes[batchSetKey] = batchSet;
				}

				if (!batchSet.TryGetValue(binKey, out var bin)) {
					bin = RenderBin.FromEntity(entity.MainEntity, inputUniformIndex);
					batchSet[binKey] = bin;
				} else {
					bin.Insert(entity.MainEntity, inputUniformIndex);
				}
				break;

			case BinnedRenderPhaseType.BatchableMesh:
				var key = (batchSetKey, binKey);
				if (!BatchableMeshes.TryGetValue(key, out var batchableBin)) {
					batchableBin = RenderBin.FromEntity(entity.MainEntity, inputUniformIndex);
					BatchableMeshes[key] = batchableBin;
				} else {
					batchableBin.Insert(entity.MainEntity, inputUniformIndex);
				}
				break;

			case BinnedRenderPhaseType.UnbatchableMesh:
				var unbatchableKey = (batchSetKey, binKey);
				if (!UnbatchableMeshes.TryGetValue(unbatchableKey, out var unbatchableBin)) {
					unbatchableBin = new UnbatchableBinnedEntities();
					unbatchableBin.Entities[entity.MainEntity] = entity.Entity;
					UnbatchableMeshes[unbatchableKey] = unbatchableBin;
				} else {
					unbatchableBin.Entities[entity.MainEntity] = entity.Entity;
				}
				break;

			case BinnedRenderPhaseType.NonMesh:
				var nonMeshKey = (batchSetKey, binKey);
				if (!NonMeshItems.TryGetValue(nonMeshKey, out var nonMeshBin)) {
					nonMeshBin = new NonMeshEntities();
					nonMeshBin.Entities[entity.MainEntity] = entity.Entity;
					NonMeshItems[nonMeshKey] = nonMeshBin;
				} else {
					nonMeshBin.Entities[entity.MainEntity] = entity.Entity;
				}
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(phaseType), phaseType, null);
		}

		UpdateCache(entity.MainEntity, new CachedBinKey<TBinKey, TBatchSetKey>(batchSetKey, binKey, phaseType), changeTick);
	}

	public void UpdateCache(MainEntity entity, CachedBinKey<TBinKey, TBatchSetKey>? cachedBinKey, uint changeTick)
	{
		var newCachedBinnedEntity = new CachedBinnedEntity<TBinKey, TBatchSetKey>(cachedBinKey, changeTick);
		var (index, oldEntry) = cachedEntityBinKeys.AddOrReplace(entity, newCachedBinnedEntity, out var replaced);
		// If the entity changed bins, record its old bin so that we can remove the entity from it
		if (replaced && oldEntry.CachedBinKey != newCachedBinnedEntity.CachedBinKey) {
			entitiesThatChangedBins.Add(new EntityThatChangedBins<TBinKey, TBatchSetKey>(entity, oldEntry));
		}
		validCachedEntityBinKeys.Set(index);
	}

	public void Render(RenderPass pass, World world, EntityView view)
	{
		var functions = world.GetRes<DrawFunctions<TBinnedPhaseItem>>();
		if (functions.HasValue) functions.Value.Prepare(world);

	}

	private void RenderBatchableMeshes(RenderPass pass, World world, EntityView view)
	{
		var drawFunctions = world.MustGetResMut<DrawFunctions<TBinnedPhaseItem>>().Value!;
		var driver = world.MustGetResMut<GraphicsDevice>();

		// don't really understand this, but its equivalent to bevys behavior. Not sure why the BatchSets would correspond
		// to both multidrawable and batchable meshes in that order.
		var zippedSets = MultidrawableMeshes.Keys.
			Concat(BatchableMeshes.Keys.Select(x => x.BatchSetKey)).Zip(BatchSets);
		foreach (var (batchSetKey, batchSet) in zippedSets) {
			var batch = batchSet.FirstBatch;
			var batchSetIndex = batchSet.Index;
			// TODO bevy does some mapping here I don't really understand

			var mappedExtraIndex = batch.ExtraIndex;
			if (mappedExtraIndex is PhaseItemExtraIndex.IndirectParametersIndex indirectParametersIndex) {
				mappedExtraIndex = new PhaseItemExtraIndex.IndirectParametersIndex(
					new Range(indirectParametersIndex.Range.Start, Index.FromStart((int)batchSet.BatchCount)),
					batchSetIndex);
			}
			var binnedPhaseItem = TBinnedPhaseItem.Create(batchSetKey, batchSet.BinKey, batch.RepresentativeEntity, batch.InstanceRange, mappedExtraIndex);

			var drawFunction = drawFunctions.Get(binnedPhaseItem.DrawFunction);
			if (drawFunction == null) continue;
			drawFunction.Draw(world, pass, view, ref binnedPhaseItem);
		}
	}

	/// <summary>
	/// Returns true if this render phase is empty.
	/// </summary>
	public bool IsEmpty =>
		MultidrawableMeshes.Count == 0 &&
		BatchableMeshes.Count == 0 &&
		UnbatchableMeshes.Count == 0 &&
		NonMeshItems.Count == 0;

	/// <summary>
	/// Prepares the render phase for a new frame by clearing internal state.
	/// </summary>
	public void PrepareForNewFrame()
	{
		BatchSets.Clear();
		validCachedEntityBinKeys.Clear();
		validCachedEntityBinKeys.EnsureCapacity(cachedEntityBinKeys.Count);
		entitiesThatChangedBins.Clear();
		foreach (var mesh in UnbatchableMeshes.Values) {
			mesh.BufferIndices.Clear();
		}
		UnbatchableMeshes.
	}
}

/// <summary>
/// All entities that share a mesh and a material and can be batched as part of
/// a BinnedRenderPhase.
/// </summary>
/// <remarks>
/// Based on Bevy's RenderBin.
/// </remarks>
public sealed class RenderBin
{
	/// <summary>
	/// A list of the entities in each bin, along with their cached <see cref="InputUniformIndex"/>
	/// </summary>
	public readonly IndexDictionary<MainEntity, InputUniformIndex> Entities = new ();

	/// <summary>
	/// Creates a RenderBin containing a single entity.
	/// </summary>
	public static RenderBin FromEntity(MainEntity entity, InputUniformIndex uniformIndex)
	{
		var bin = new RenderBin();
		bin.Entities[entity] = uniformIndex;
		return bin;
	}

	/// <summary>
	/// Inserts an entity into the bin.
	/// </summary>
	public void Insert(MainEntity entity, InputUniformIndex uniformIndex)
	{
		Entities[entity] = uniformIndex;
	}

	/// <summary>
	/// Removes an entity from the bin.
	/// </summary>
	public bool Remove(MainEntity entityToRemove)
	{
		return Entities.Remove(entityToRemove);
	}

	/// <summary>
	/// Returns true if the bin contains no entities.
	/// </summary>
	public bool IsEmpty => Entities.Count == 0;
}

/// <summary>
/// Information that we track about an entity that was in one bin on the previous frame and is in a different bin this
/// frame
/// </summary>
/// <param name="MainEntity">The entity</param>
/// <param name="OldCachedBinnedEntity">The key that identifies the bin that this entity used to be in</param>
public record struct EntityThatChangedBins<TBinKey, TBatchSetKey>(
	MainEntity MainEntity,
	CachedBinnedEntity<TBinKey, TBatchSetKey> OldCachedBinnedEntity)
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey { }

/// <summary>
/// Information that we keep about an entity currently within a bin
/// </summary>
/// <param name="CachedBinKey">Information that we use to identify a cached entity in a bin</param>
/// <param name="ChangeTick">Last modified tick of the entity. We use this to detect when the entity needs to be invalidated</param>
public record struct CachedBinnedEntity<TBinKey, TBatchSetKey>(CachedBinKey<TBinKey, TBatchSetKey>? CachedBinKey, uint ChangeTick)
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey;

/// <summary>
/// Information that we use to identify a cached entity in a bin
/// </summary>
/// <param name="BatchSetKey">The key of the batch set containing the entity</param>
/// <param name="BinKey">The key of the bin containing the entity</param>
/// <param name="PhaseType">The type of render phase that we use to render the entity</param>
/// <typeparam name="TBinKey"></typeparam>
/// <typeparam name="TBatchSetKey"></typeparam>
public record struct CachedBinKey<TBinKey, TBatchSetKey>(
	TBatchSetKey BatchSetKey,
	TBinKey BinKey,
	BinnedRenderPhaseType PhaseType)
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey;

/// <summary>
/// A group of entities that will be batched together into a single multi-draw call
/// </summary>
/// <typeparam name="TBinKey"></typeparam>
public struct BinnedRenderPhaseBatchSet<TBinKey>
{
	/// <summary>
	/// The first batch in this batch set
	/// </summary>
	public BinnedRenderPhaseBatch FirstBatch;
	/// <summary>
	/// The key of the bin that the first batch corresponds to
	/// </summary>
	public TBinKey BinKey;
	/// <summary>
	/// The number of batches 
	/// </summary>
	public uint BatchCount;
	/// <summary>
	/// The index of the batch set in the gpu buffer
	/// </summary>
	public uint Index;
}

/// <summary>
/// Information about a single batch of entities rendered using binned phase items.
/// </summary>
/// <remarks>
/// Based on Bevy's BinnedRenderPhaseBatch.
/// </remarks>
/// <param name="RepresentativeEntity">An entity that's representative of this batch. Verse uses this to fetch the mesh. It can be any entity in the batch</param>
/// <param name="InstanceRange">The range of instance indices in this batch</param>
/// <param name="ExtraIndex">The dynamic offset of the batch. Note that dynamic offsets are only used on platforms that don't support storage buffers</param>
public readonly record struct BinnedRenderPhaseBatch(
	(ulong Entity, MainEntity MainEntity) RepresentativeEntity,
	Range InstanceRange,
	PhaseItemExtraIndex ExtraIndex
);

/// <summary>
/// Extra information associated with some PhaseItems, alongside the indirect instance index.
/// </summary>
/// <remarks>
/// Sometimes phase items require another index in addition to the range of instances they already have.
/// These can be:
/// - The dynamic offset: a dynamic offset into the uniform buffer of instance data. This is used on
///   platforms that don't support storage buffers, to work around uniform buffer size limitations.
/// - The indirect parameters index: an index into the buffer that specifies the indirect parameters
///   for this PhaseItem's drawcall. This is used when indirect mode is on (as used for GPU culling).
///
/// Note that our indirect draw functionality requires storage buffers, so it's impossible to have both
/// a dynamic offset and an indirect parameters index. This convenient fact allows us to pack both indices
/// into a discriminated union.
///
/// Based on Bevy's PhaseItemExtraIndex.
/// </remarks>
public abstract record PhaseItemExtraIndex
{
	/// <summary>
	/// No extra index is present.
	/// </summary>
	public sealed record None : PhaseItemExtraIndex;

	/// <summary>
	/// A dynamic offset into the uniform buffer of instance data. This is used on platforms
	/// that don't support storage buffers, to work around uniform buffer size limitations.
	/// </summary>
	public sealed record DynamicOffset(uint Offset) : PhaseItemExtraIndex;

	/// <summary>
	/// An index into the buffer that specifies the indirect parameters for this PhaseItem's drawcall.
	/// This is used when indirect mode is on (as used for GPU culling).
	/// </summary>
	public sealed record IndirectParametersIndex(Range Range, uint? BatchSetIndex = null) : PhaseItemExtraIndex;

	/// <summary>
	/// Returns either an indirect parameters index or None, as appropriate.
	/// </summary>
	public static PhaseItemExtraIndex MaybeIndirectParametersIndex(uint? indirectParametersIndex) =>
		indirectParametersIndex.HasValue
			? new IndirectParametersIndex(new Range((int)indirectParametersIndex.Value, (int)(indirectParametersIndex.Value + 1)))
			: new None();

	/// <summary>
	/// Returns either a dynamic offset index or None, as appropriate.
	/// </summary>
	public static PhaseItemExtraIndex MaybeDynamicOffset(uint? dynamicOffset) =>
		dynamicOffset.HasValue
			? new DynamicOffset(dynamicOffset.Value)
			: new None();
}

/// <summary>
/// Identifies the list within BinnedRenderPhase that a phase item is to be placed in.
/// </summary>
/// <remarks>
/// Based on Bevy's BinnedRenderPhaseType.
/// </remarks>
public enum BinnedRenderPhaseType
{
	/// <summary>
	/// The item is a mesh that's eligible for multi-draw indirect rendering and
	/// can be batched with other meshes of the same type.
	/// </summary>
	MultidrawableMesh,

	/// <summary>
	/// The item is a mesh that can be batched with other meshes of the same type and
	/// drawn in a single draw call.
	/// </summary>
	BatchableMesh,

	/// <summary>
	/// The item is a mesh that's eligible for indirect rendering, but can't be
	/// batched with other meshes of the same type.
	/// </summary>
	UnbatchableMesh,

	/// <summary>
	/// The item isn't a mesh at all.
	/// </summary>
	/// <remarks>
	/// Verse will simply invoke the drawing commands for such items one after another,
	/// with no further processing. The engine itself doesn't enqueue any items of this type,
	/// but it's available for use in your application and/or plugins.
	/// </remarks>
	NonMesh
}

/// <summary>
/// The index of the uniform describing this object in the GPU buffer, when GPU preprocessing is enabled.
/// </summary>
/// <remarks>
/// For example, for 3D meshes, this is the index of the MeshInputUniform in the buffer.
/// This field is ignored if GPU preprocessing isn't in use. In that case, it can be safely
/// set to default.
///
/// Based on Bevy's InputUniformIndex.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct InputUniformIndex(uint Index)
{
	public static implicit operator uint(InputUniformIndex index) => index.Index;
	public static implicit operator InputUniformIndex(uint index) => new (index);
}

/// <summary>
/// An item (entity of the render world) which will be drawn to a texture or the screen,
/// as part of a render phase.
/// </summary>
/// <remarks>
/// The data required for rendering an entity is extracted from the main world in the
/// ExtractSchedule. Then it has to be queued up for rendering during the Queue systems,
/// by adding a corresponding phase item to a render phase. Afterwards it will be possibly
/// sorted and rendered automatically in the PhaseSort and Render systems, respectively.
///
/// PhaseItems come in two flavors: BinnedPhaseItems and SortedPhaseItems.
///
/// - Binned phase items have a BinKey which specifies what bin they're to be placed in.
///   All items in the same bin are eligible to be batched together. The BinKeys are sorted,
///   but the individual bin items aren't. Binned phase items are good for opaque meshes,
///   in which the order of rendering isn't important. Generally, binned phase items are
///   faster than sorted phase items.
///
/// - Sorted phase items, on the other hand, are placed into one large buffer and then
///   sorted all at once. This is needed for transparent meshes, which have to be sorted
///   back-to-front to render with the painter's algorithm. These types of phase items are
///   generally slower than binned phase items.
///
/// Based on Bevy's PhaseItem trait.
/// </remarks>
public interface IPhaseItem
{
	/// <summary>
	/// Whether or not this PhaseItem should be subjected to automatic batching. (Default: true)
	/// </summary>
	bool AutomaticBatching => true;

	/// <summary>
	/// The corresponding entity that will be drawn.
	/// </summary>
	/// <remarks>
	/// This is used to fetch the render data of the entity, required by the draw function,
	/// from the render world.
	/// </remarks>
	ulong Entity { get; }

	/// <summary>
	/// The main world entity represented by this PhaseItem.
	/// </summary>
	MainEntity MainEntity { get; }

	/// <summary>
	/// Specifies the Draw function used to render the item.
	/// </summary>
	int DrawFunction { get; }

	/// <summary>
	/// The range of instances that the batch covers. After doing a batched draw, batch range
	/// length phase items will be skipped. This design is to avoid having to restructure the
	/// render phase unnecessarily.
	/// </summary>
	ref Range BatchRange { get; }

	/// <summary>
	/// Returns the PhaseItemExtraIndex.
	/// </summary>
	/// <remarks>
	/// If present, this is either a dynamic offset or an indirect parameters index.
	/// </remarks>
	PhaseItemExtraIndex ExtraIndex { get; }
}

/// <summary>
/// A key used to combine batches into batch sets.
/// </summary>
/// <remarks>
/// A batch set is a set of meshes that can potentially be multi-drawn together.
/// Based on Bevy's PhaseItemBatchSetKey trait.
/// </remarks>
public interface IPhaseItemBatchSetKey : IComparable<IPhaseItemBatchSetKey>, IEquatable<IPhaseItemBatchSetKey>
{
	/// <summary>
	/// Returns true if this batch set key describes indexed meshes or false if
	/// it describes non-indexed meshes.
	/// </summary>
	/// <remarks>
	/// Verse uses this in order to determine which kind of indirect draw parameters
	/// to use, if indirect drawing is enabled.
	/// </remarks>
	bool Indexed { get; }
}

/// <summary>
/// Represents phase items that are placed into bins. The BinKey specifies which bin they're
/// to be placed in. Bin keys are sorted, and items within the same bin are eligible to be
/// batched together. The elements within the bins aren't themselves sorted.
/// </summary>
/// <remarks>
/// An example of a binned phase item is Opaque3d, for which the rendering order isn't critical.
/// Based on Bevy's BinnedPhaseItem trait.
/// </remarks>
/// <typeparam name="TBinKey">
/// <summary>
/// The key used for binning PhaseItems into bins. Order the members of BinKey by the order
/// of binding for best performance. For example, pipeline id, draw function id, mesh asset id,
/// lowest variable bind group id such as the material bind group id, and its dynamic offsets
/// if any, next bind group and offsets, etc. This reduces the need for rebinding between bins
/// and improves performance.
/// </summary>
/// </typeparam>
/// <typeparam name="TBatchSetKey">
/// <summary>
/// The key used to combine batches into batch sets.
/// </summary>
/// <remarks>
/// A batch set is a set of meshes that can potentially be multi-drawn together.
/// </remarks>
/// </typeparam>
/// <typeparam name="TOut">The concrete instance of IBinnedPhaseItem to minimize boxing during creation</typeparam>
public interface IBinnedPhaseItem<in TBinKey, in TBatchSetKey, out TOut> : IPhaseItem
	where TBinKey : IComparable, IEquatable<TBinKey>
	where TBatchSetKey : IPhaseItemBatchSetKey
	where TOut : IBinnedPhaseItem<TBinKey, TBatchSetKey, TOut>
{
	/// <summary>
	/// Create a new binned phase item from the key and per-entity data
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="ISortedPhaseItem"/>s, this is generally called "just in time"
	/// before rendering. The resulting phase item isn't stored in any data structures, resulting in significant memory
	/// savings
	/// </remarks>
	/// <returns></returns>
	public static abstract TOut Create(
		TBatchSetKey batchSetKey, TBinKey binKey, (ulong, MainEntity) representativeEntity, Range batchRange, PhaseItemExtraIndex extraIndex);
}

/// <summary>
/// Represents phase items that must be sorted. The SortKey specifies the order that these
/// items are drawn in. These are placed into a single array, and the array as a whole is
/// then sorted.
/// </summary>
/// <remarks>
/// An example of a sorted phase item is Transparent3d, which must be sorted back to front
/// in order to correctly render with the painter's algorithm.
/// Based on Bevy's SortedPhaseItem trait.
/// </remarks>
public interface ISortedPhaseItem : IPhaseItem
{
	/// <summary>
	/// The type used for ordering the items. The smallest values are drawn first.
	/// This order can be calculated using the ViewRangefinder3d, based on the view-space
	/// Z value of the corresponding view matrix.
	/// </summary>
	IComparable SortKey { get; }

	/// <summary>
	/// Whether this phase item targets indexed meshes (those with both vertex and index buffers
	/// as opposed to just vertex buffers).
	/// </summary>
	/// <remarks>
	/// Verse needs this information in order to properly group phase items together for
	/// multi-draw indirect, because the GPU layout of indirect commands differs between
	/// indexed and non-indexed meshes.
	///
	/// If you're implementing a custom phase item that doesn't describe a mesh, you can
	/// safely return false here.
	/// </remarks>
	bool Indexed { get; }
}

/// <summary>
/// Information about the unbatchable entities in a bin.
/// </summary>
/// <remarks>
/// Based on Bevy's UnbatchableBinnedEntities.
/// </remarks>
public sealed class UnbatchableBinnedEntities
{
	/// <summary>
	/// The entities.
	/// </summary>
	public Dictionary<MainEntity, ulong> Entities { get; } = new ();
	/// <summary>
	/// The GPU array buffer indices of each unbatchable binned entity.
	/// </summary>
	internal UnbatchableBinnedEntityIndexSet BufferIndices = new UnbatchableBinnedEntityIndexSet.NoEntities();
}

public abstract record UnbatchableBinnedEntityIndexSet
{
	public record NoEntities() : UnbatchableBinnedEntityIndexSet;
	public record Sparse(Range InstanceRange, uint FirstIndirectParametersIndex) : UnbatchableBinnedEntityIndexSet;
	public record Dense(List<UnbatchableBinnedEntityIndices> Indices) : UnbatchableBinnedEntityIndexSet;

	public void Clear()
	{
		if (this is Sparse sparse) {
			this = new NoEntities();
		}
	}
}

public record struct UnbatchableBinnedEntityIndices(uint InstanceIndex, PhaseItemExtraIndex ExtraIndex);

/// <summary>
/// Information about non-mesh entities.
/// </summary>
/// <remarks>
/// Based on Bevy's NonMeshEntities.
/// </remarks>
public sealed class NonMeshEntities
{
	/// <summary>
	/// The entities.
	/// </summary>
	public Dictionary<MainEntity, ulong> Entities { get; } = new ();
}

/// <summary>
/// Stores the rendering instructions for a single phase that sorts items in all views.
/// </summary>
/// <remarks>
/// They're cleared out every frame, but storing them in a resource like this allows
/// us to reuse allocations.
///
/// Based on Bevy's ViewSortedRenderPhases.
/// </remarks>
/// <typeparam name="TSortedPhaseItem">The type of sorted phase item.</typeparam>
public sealed class ViewSortedRenderPhases<TSortedPhaseItem> : Dictionary<RetainedViewEntity, SortedRenderPhase<TSortedPhaseItem>>
	where TSortedPhaseItem : ISortedPhaseItem
{
	/// <summary>
	/// Inserts or clears the render phase for the given view.
	/// </summary>
	public void InsertOrClear(RetainedViewEntity retainedViewEntity)
	{
		if (TryGetValue(retainedViewEntity, out var existingPhase)) {
			existingPhase.Clear();
		} else {
			this[retainedViewEntity] = new SortedRenderPhase<TSortedPhaseItem>();
		}
	}
}

/// <summary>
/// A collection of all items to be rendered that will be encoded to GPU commands for a
/// single render phase for a single view.
/// </summary>
/// <remarks>
/// <para>
/// Each view (camera, or shadow-casting light, etc.) can have one or multiple render phases.
/// They are used to queue entities for rendering. Multiple phases might be required due to
/// different sorting/batching behaviors (e.g. opaque: front to back, transparent: back to front)
/// or because one phase depends on the rendered texture of the previous phase (e.g. for
/// screen-space reflections). All PhaseItems are then rendered using a single TrackedRenderPass.
/// The render pass might be reused for multiple phases to reduce GPU overhead.
/// </para>
/// <para>
/// This flavor of render phase is used only for meshes that need to be sorted back-to-front,
/// such as transparent meshes. For items that don't need strict sorting, BinnedRenderPhase
/// is preferred, for performance.
/// </para>
/// <para>
/// Based on Bevy's SortedRenderPhase.
/// </para>
/// </remarks>
/// <typeparam name="TSortedPhaseItem">The type of sorted phase item.</typeparam>
public sealed class SortedRenderPhase<TSortedPhaseItem>
	where TSortedPhaseItem : ISortedPhaseItem
{
	/// <summary>
	/// The items within this SortedRenderPhase.
	/// </summary>
	public List<TSortedPhaseItem> Items { get; } = new ();

	/// <summary>
	/// Adds a PhaseItem to this render phase.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Add(TSortedPhaseItem item)
	{
		Items.Add(item);
	}

	/// <summary>
	/// Removes all PhaseItems from this render phase.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear()
	{
		Items.Clear();
	}

	/// <summary>
	/// Sorts all of its PhaseItems.
	/// </summary>
	public void Sort()
	{
		// Use stable sort for consistent results when sort keys are equal
		Items.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
	}

	/// <summary>
	/// An iterator through the associated Entity for each PhaseItem in order.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IEnumerable<ulong> IterEntities()
	{
		return Items.Select(item => item.Entity);
	}

	/// <summary>
	/// Returns the number of items in this render phase.
	/// </summary>
	public int Count => Items.Count;

	/// <summary>
	/// Returns true if this render phase is empty.
	/// </summary>
	public bool IsEmpty => Items.Count == 0;
}

/// <summary>
/// Convenience methods for working with render phases.
/// </summary>
/// <remarks>
/// Based on Bevy's render phase utility functions.
/// </remarks>
public static class RenderPhaseExtensions
{
	/// <summary>
	/// Determines the appropriate BinnedRenderPhaseType for a mesh based on its batching capabilities
	/// and GPU preprocessing support.
	/// </summary>
	/// <param name="batchable">Whether the mesh supports batching.</param>
	/// <param name="supportsMultiDraw">Whether the platform supports multi-draw indirect.</param>
	/// <returns>The appropriate BinnedRenderPhaseType.</returns>
	public static BinnedRenderPhaseType GetMeshPhaseType(bool batchable, bool supportsMultiDraw = true)
	{
		return (batchable, supportsMultiDraw) switch {
			(true, true)  => BinnedRenderPhaseType.MultidrawableMesh,
			(true, false) => BinnedRenderPhaseType.BatchableMesh,
			(false, _)    => BinnedRenderPhaseType.UnbatchableMesh
		};
	}
}