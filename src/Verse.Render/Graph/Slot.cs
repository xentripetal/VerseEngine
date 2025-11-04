namespace Verse.Render.Graph;

using Verse.MoonWorks.Graphics.Resources;

public enum SlotType
{
	Buffer,
	Texture,
	Sampler,
	Entity
}

/// <summary>
/// Types of render slots for connecting nodes
/// </summary>
public record struct SlotValue : IIntoSlotValue
{
	private SlotValue(SlotType type, object value)
	{
		Type = type;
		Value = value;
	}
	public readonly SlotType Type;
	public readonly object Value;

	public Buffer GetBuffer() => (Buffer)Value;
	public Texture GetTexture() => (Texture)Value;
	public Sampler GetSampler() => (Sampler)Value;
	public ulong GetEntity() => (ulong)Value;

	/// <summary>Render buffer for vertex data, see <see cref="Verse.MoonWorks.Graphics.Resources.Buffer"/></summary>
	public static SlotValue OfBuffer(Buffer buffer) => new (SlotType.Buffer, buffer);

	/// <summary>SDL3 texture/surface. See <see cref="Texture"/></summary>
	public static SlotValue OfTexture(Texture texture) => new (SlotType.Texture, texture);


	/// <summary>Sampler defines how a pipeline will sample from a <see cref="Texture"/></summary>
	public static SlotValue OfSampler(Sampler sampler) => new (SlotType.Sampler, sampler);

	/// <summary>Entity/component data</summary>
	public static SlotValue OfEntity(ulong entity) => new (SlotType.Entity, entity);

	public SlotValue IntoSlotValue() => this;
}

public interface IIntoSlotValue
{
	public SlotValue IntoSlotValue();
}

/// <summary>
/// Information about a render slot
/// </summary>
public record struct SlotInfo(string Name, SlotType Type);

/// <summary>
/// A reference to a slot either by its name or index inside the <see cref="RenderGraph"/>
/// </summary>
public record struct SlotLabel : IIntoSlotLabel
{
	private SlotLabel(int index)
	{
		_index = index;
	}
	private SlotLabel(string name)
	{
		_name = name;
		_index = -1;
	}
	private int _index;
	private string? _name;
	public static SlotLabel OfIndex(int index) => new SlotLabel(index);
	public static SlotLabel OfName(string name) => new SlotLabel(name);

	public bool IsName => _name != null;
	public bool IsIndex => _index >= 0;

	public SlotLabel IntoSlotLabel() => this;

	public int IndexOf(IReadOnlyList<SlotInfo> slots)
	{
		if (IsIndex) {
			return _index;
		}
		if (IsName) {
			for (var i = 0; i < slots.Count; i++) {
				if (slots[i].Name == _name) {
					return i;
				}
			}
		}
		return -1;
	}
	
	public SlotInfo? GetSlotInfo(IReadOnlyList<SlotInfo> slots)
	{
		var index = IndexOf(slots);
		if (index < 0 || index >= slots.Count) {
			return null;
		}
		return slots[index];
	}
}

public interface IIntoSlotLabel
{
	public SlotLabel IntoSlotLabel();
}