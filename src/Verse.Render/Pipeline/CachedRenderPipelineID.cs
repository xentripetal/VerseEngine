namespace Verse.Render.Pipeline;


public struct CachedRenderPipelineId(uint Id)
{
	public static CachedRenderPipelineId Invalid => new CachedRenderPipelineId(uint.MaxValue);
}