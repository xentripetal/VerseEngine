namespace Verse.ECS;

/// <summary>
/// Marks a partial class or struct for label generation.
/// </summary>
/// <typeparam name="T"></typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class LabelAttribute<T> : Attribute where T : ILabel { }

/// <summary>
/// Trait for specific label interfaces to implement.
/// </summary>
public interface ILabel : IEquatable<ILabel>
{
	public string GetLabelName();
}

/// <summary>
/// Base type for <see cref="ILabel"/>s derived from an enum. 
/// </summary>
/// <param name="value"></param>
public abstract class LabelEnum(Enum value) : ILabel
{
	public static implicit operator Enum(LabelEnum e) => e.Value;
	public Enum Value => value;
	public string GetLabelName() => Value.ToString();
	public bool Equals(ILabel? other) => other is LabelEnum e && e.Value.Equals(Value);
	public override bool Equals(object? obj) => obj is LabelEnum e && e.Value.Equals(Value);
	public override int GetHashCode() => HashCode.Combine(Value, GetType());
}