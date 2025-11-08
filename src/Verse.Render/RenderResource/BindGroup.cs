namespace Verse.Render.RenderResource;

public record struct BindGroupId(uint Id) : IComparable<BindGroupId>
{
	public int CompareTo(BindGroupId other) => Id.CompareTo(other.Id);
}