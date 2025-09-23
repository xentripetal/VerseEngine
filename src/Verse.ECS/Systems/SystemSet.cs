using Verse.ECS.Scheduling.Configs;

namespace Verse.ECS.Systems;

public interface ISystemSet : IEquatable<ISystemSet>, IIntoSystemSet, IIntoSystemSetConfigs
{
	/// <summary>
	/// The name of the set.
	/// </summary>
	/// <returns></returns>
	public string GetName();
	/// <summary>
	/// Whether the set represents a single system. 
	/// </summary>
	/// <returns></returns>
	public bool IsSystemAlias();
}

/// <summary>
/// Helpers for creating system sets from various types.
/// </summary>
public static class SystemSet
{
	public static IIntoSystemSetConfigs Of(Enum first, params Enum[] enums) => EnumSets.Of(enums.Prepend(first));
	public static IIntoSystemSetConfigs Of(ISystem first, params ISystem[] systems) =>
		NodeConfigs<ISystemSet>.Of(systems.Prepend(first).Select(IIntoSystemSetConfigs (x) => new SystemReferenceSet(x)));
	public static IIntoSystemSetConfigs Of(ISystemSet first, params ISystemSet[] sets) => NodeConfigs<ISystemSet>.Of(sets.Prepend(first));
	public static IIntoSystemSetConfigs Of(ISystemSetLabel first, params ISystemSetLabel[] labels) =>
		NodeConfigs<ISystemSet>.Of(labels.Prepend(first).Select(IIntoSystemSetConfigs (l) => new SystemSetLabel(l)));
	public static IIntoSystemSetConfigs Of(IIntoSystemSet first, params IIntoSystemSet[] sets) =>
		NodeConfigs<ISystemSet>.Of(sets.Prepend(first).Select(x => x.IntoSystemSet()));
}

public readonly record struct NamedSet(string Name) : ISystemSet
{
	public bool IsSystemAlias() => false;

	public bool Equals(ISystemSet? other)
	{
		if (other is NamedSet otherNamed) {
			return Name == otherNamed.Name;
		}
		return false;
	}

	public string GetName() => Name;
	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);

	public override int GetHashCode() => Name.GetHashCode();
}

public readonly struct AnonymousSet(ulong id) : ISystemSet
{
	public readonly ulong Id = id;

	public bool Equals(ISystemSet? other)
	{
		if (other is AnonymousSet otherAnonymous) {
			return Id == otherAnonymous.Id;
		}
		return false;
	}

	public override int GetHashCode() => Id.GetHashCode();

	public bool IsSystemAlias() => false;

	public string GetName() => $"AnonymousSet {Id}";
	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);
}

/// <summary>
///     A <see cref="ISystemSet" /> grouping instances of the same <see cref="ISystem" />.
///     This kind of set is automatically populated and thus has some special rules:
///     <list type="bullet">
///         <item>You cannot manually add members</item>
///         <item>You cannot configure them</item>
///         <item>You cannot order something relative to one if it has more than one member</item>
///     </list>
/// </summary>
public class SystemReferenceSet(ISystem sys) : ISystemSet
{
	public readonly ISystem System = sys;

	public bool Equals(ISystemSet? other)
	{
		if (other is SystemReferenceSet otherType) {
			return ReferenceEquals(System, otherType.System);
		}
		return false;
	}

	public string GetName() => $"SystemReferenceSet {System.GetType().Name}";

	public bool IsSystemAlias() => true;

	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);

	public override int GetHashCode() => System.GetHashCode();
}

public class SystemTypeSet : ISystemSet
{
	public SystemTypeSet(Type type)
	{
		// check if type implements ISystem interface
		if (!typeof(ISystem).IsAssignableFrom(type))
			throw new ArgumentException("Type must implement ISystem interface", nameof(type));
		Type = type;
	}

	public Type Type { get; }

	public bool Equals(ISystemSet? other)
	{
		if (other is SystemTypeSet otherType) {
			return Type == otherType.Type;
		}
		return false;
	}

	public ISystemSet IntoSystemSet() => this;

	public string GetName() => $"SystemTypeSet {Type.Name}";

	public bool IsSystemAlias() => true;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);

	public override int GetHashCode() => Type.GetHashCode();
}

public class SystemTypeSet<T>() : SystemTypeSet(typeof(T))
	where T : ISystem
{
	public new bool Equals(ISystemSet? other)
	{
		if (other is SystemTypeSet<T> otherType) {
			return true;
		}
		return base.Equals(other);
	}

	public new string GetName() => $"SystemTypeSet {typeof(T).Name}";

	public new ISystemSet IntoSystemSet() => this;
}

public struct EnumSets(params Enum[] Sets) : IIntoSystemSetConfigs
{
	public static IIntoSystemSetConfigs Of(params Enum[] sets) => new EnumSets(sets);
	public static IIntoSystemSetConfigs Of(IEnumerable<Enum> sets) => new EnumSets(sets.ToArray());
	public NodeConfigs<ISystemSet> IntoConfigs() => NodeConfigs<ISystemSet>.Of(Sets.Select(EnumSet.Of));

	public static implicit operator EnumSets(Enum[] sets) => new EnumSets(sets);
	public static implicit operator EnumSets(List<Enum> sets) => new EnumSets(sets.ToArray());
}

public class EnumSet(Enum value) : ISystemSet
{
	public static ISystemSet Of(Enum set) => new EnumSet(set);
	public static IIntoSystemSetConfigs Of(params Enum[] sets) => new EnumSets(sets);

	public static implicit operator EnumSet(Enum value) => new EnumSet(value);
	public static implicit operator Enum(EnumSet set) => set.Value;

	public Enum Value { get; } = value;

	public bool Equals(ISystemSet? other)
	{
		if (other is EnumSet otherEnum) {
			return Value.Equals(otherEnum.Value);
		}
		return false;
	}

	public ISystemSet IntoSystemSet() => this;

	public string GetName() => Value.ToString();

	public bool IsSystemAlias() => false;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);

	public override int GetHashCode() => HashCode.Combine(Value);
}

/// <summary>
///     A system set that is defined by its type. All instances of the same type are part of the same set.
/// </summary>
public abstract class StaticSystemSet : ISystemSet
{
	public bool Equals(ISystemSet? other) => other is StaticSystemSet && other.GetType() == GetType();

	public string GetName() => GetType().Name;

	public bool IsSystemAlias() => false;
	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);

	public override int GetHashCode() => GetType().GetHashCode();
}

public interface ISystemSetLabel : ILabel { }

public class SystemSetLabel(ISystemSetLabel label) : ISystemSet
{
	public readonly ISystemSetLabel Label = label;
	public bool Equals(ISystemSet? other) => other is SystemSetLabel l && l.Label.Equals(Label);
	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);
	public string GetName() => label.GetLabelName();
	public bool IsSystemAlias() => false;
	public override int GetHashCode() => Label.GetHashCode();
}