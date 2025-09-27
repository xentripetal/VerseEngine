using Verse.Render.Graph.RenderPhase;
using Verse.Render.Pipeline;

namespace Verse.Render.Pipeline2D;

/// <summary>
/// 2D meshes aren't currently multi-drawn together, so this batch set key only stores whether the mesh is indexed
/// </summary>
/// <param name="Indexed"></param>
public record struct BatchSetKey2D(bool Indexed) : IPhaseItemBatchSetKey { }

public record struct Opaque2DBinkey(CachedRenderPipelineId Pipeline, DrawFunctionId DrawFunction, )
public struct Opaque2D
{
	public BatchSetKey2D BatchSetKey;
	public 
}