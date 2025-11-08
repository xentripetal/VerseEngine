using Verse.Assets;
using Verse.ECS;
using Verse.Render.Graph.RenderPhase;
using Verse.Render.Pipeline;
using Verse.Render.RenderResource;

namespace Verse.Render.Pipeline2D;

/// <summary>
/// 2D meshes aren't currently multi-drawn together, so this batch set key only stores whether the mesh is indexed
/// </summary>
/// <param name="Indexed"></param>
public record struct BatchSetKey2D(bool Indexed) : IPhaseItemBatchSetKey { }

public record struct Opaque2DBinkey(CachedRenderPipelineId Pipeline, DrawFunctionId DrawFunction, UntypedAssetId AssetId, BindGroupId? MaterialBindGroupId)
	: IComparable<Opaque2DBinkey>
{
	public int CompareTo(Opaque2DBinkey other)
	{
		var pipelineCmd = Pipeline.Id.CompareTo(other.Pipeline.Id);
		if (pipelineCmd != 0) return pipelineCmd;
		var drawFunctionCmd = DrawFunction.Id.CompareTo(other.DrawFunction.Id);
		if (drawFunctionCmd != 0) return drawFunctionCmd;
		var assetCmd = AssetId.CompareTo(other.AssetId);
		if (assetCmd != 0) return assetCmd;
		if (MaterialBindGroupId.HasValue && other.MaterialBindGroupId.HasValue) {
			return MaterialBindGroupId.Value.CompareTo(other.MaterialBindGroupId.Value);
		}
		return MaterialBindGroupId.HasValue ? 1 : -1;
	}
}

public ref struct Opaque2D : IBinnedPhaseItem<Opaque2DBinkey, BatchSetKey2D, Opaque2D>
{
	public BatchSetKey2D BatchSetKey;
	public Opaque2DBinkey BinKey;
	public (ulong, MainEntity) RepresentativeEntity;
	public Range BatchRange {
		get;
		set;
	}
	public PhaseItemExtraIndex ExtraIndex { get; set; }
	public bool AutomaticBatching { get; }
	public ulong Entity => RepresentativeEntity.Item1;
	public MainEntity MainEntity => RepresentativeEntity.Item2;
	public DrawFunctionId DrawFunction => BinKey.DrawFunction;

	public static Opaque2D Create(
		BatchSetKey2D batchSetKey, Opaque2DBinkey binKey, (ulong, MainEntity) representativeEntity, Range batchRange, PhaseItemExtraIndex extraIndex)
	{
		return new Opaque2D {
			BatchSetKey = batchSetKey,
			BinKey = binKey,
			RepresentativeEntity = representativeEntity,
			BatchRange = batchRange,
			ExtraIndex = extraIndex
		};
	}
}