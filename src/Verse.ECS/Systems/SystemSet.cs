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

public class MethodSystemSet<T>(string Method) : ISystemSet
{
	public bool Equals(ISystemSet? other)
	{
		if (other is MethodSystemSet<T> otherFn)
			return other.GetName() == GetName();
		return false;
	}
	public ISystemSet IntoSystemSet() => this;
	public NodeConfigs<ISystemSet> IntoConfigs() => new SystemSetConfig(this);
	public string GetName() => typeof(T).Name + $".{Method}()";
	public bool IsSystemAlias() => true;
}

public struct EnumSets<T>(params T[] Sets) : IIntoSystemSetConfigs where T : struct, Enum
{
	public static EnumSets<T> Of(params T[] sets) => new EnumSets<T>(sets);
	public NodeConfigs<ISystemSet> IntoConfigs() => NodeConfigs<ISystemSet>.Of(Sets.Select(EnumSet<T>.Of));
	
	// re-export all the interface methods from IIntoSystemConfigs to make it easier to chain them
	#region IIntoSystemConfigs
	public NodeConfigs<ISystemSet> InSet(IIntoSystemSet set) => IntoConfigs().InSet(set);

	public NodeConfigs<ISystemSet> InSet<ISystemSetEnum>(ISystemSetEnum set) where ISystemSetEnum : struct, Enum => IntoConfigs().InSet(set);

	public NodeConfigs<ISystemSet> Before(IIntoSystemSet set) => IntoConfigs().Before(set);

	public NodeConfigs<ISystemSet> After(IIntoSystemSet set) => IntoConfigs().After(set);

	public NodeConfigs<ISystemSet> BeforeIgnoreDeferred(IIntoSystemSet set) => IntoConfigs().BeforeIgnoreDeferred(set);

	public NodeConfigs<ISystemSet> AfterIgnoreDeferred(IIntoSystemSet set) => IntoConfigs().AfterIgnoreDeferred(set);

	public NodeConfigs<ISystemSet> Chained() => IntoConfigs().Chained();

	public NodeConfigs<ISystemSet> ChainedIgnoreDeferred() => IntoConfigs().ChainedIgnoreDeferred();

	public NodeConfigs<ISystemSet> RunIf(ICondition condition) => IntoConfigs().RunIf(condition);

	public NodeConfigs<ISystemSet> DistributiveRunIf(ICondition condition) => IntoConfigs().DistributiveRunIf(condition);

	public NodeConfigs<ISystemSet> AmbiguousWith(IIntoSystemSet set) => IntoConfigs().AmbiguousWith(set);

	public NodeConfigs<ISystemSet> AmbiguousWithAll() => IntoConfigs().AmbiguousWithAll();
	#endregion
}

public class EnumSet<T> : ISystemSet where T : struct, Enum
{
	public EnumSet(T set)
	{
		Value = set;
	}

	public static EnumSet<T> Of(T set) => new (set);

	public T Value { get; }

	public bool Equals(ISystemSet? other)
	{
		if (other is EnumSet<T> otherEnum) {
			return Value.Equals(otherEnum.Value);
		}
		return false;
	}

	public ISystemSet IntoSystemSet() => this;

	public string GetName() => $"{typeof(T).Name}({Enum.GetName(Value)})";

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