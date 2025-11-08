namespace Verse.Render.Pipeline;


public record struct CachedRenderPipelineId(uint Id)
{
	public static CachedRenderPipelineId Invalid => new CachedRenderPipelineId(uint.MaxValue);
}