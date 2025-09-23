using System.Reflection;
using Verse.ECS.Scheduling.Configs;
using Verse.ECS.Systems;

namespace Verse.ECS;

public abstract class SystemInitAttribute : Attribute
{
	public abstract void OnInit(ISystem system, World world);

	public static void ApplyAll(ISystem system, World world, Attribute[] attributes)
	{
		foreach (var attr in attributes) {
			if (attr is SystemInitAttribute initAttr)
				initAttr.OnInit(system, world);
		}
	}
}

public class WithReadAttribute<T> : SystemInitAttribute where T : struct
{

	public override void OnInit(ISystem system, World world)
	{
		var id = world.GetComponent<T>();
		system.Meta.Access.AddUnfilteredRead(id.Id);
	}
}

public class WithWriteAttribute<T> : SystemInitAttribute where T : struct
{
	public override void OnInit(ISystem system, World world)
	{
		var id = world.GetComponent<T>();
		system.Meta.Access.AddUnfilteredWrite(id.Id);
	}
}

public class ExclusiveSystemAttribute : SystemInitAttribute
{

	public override void OnInit(ISystem system, World world)
	{
		system.Meta.IsExclusive = true;
	}
}

public abstract class SystemConfigAttribute : Attribute
{
	public abstract IIntoSystemConfigs Apply(IIntoSystemConfigs configs);

	public static IIntoSystemConfigs ApplyAllFromMethod(IIntoSystemConfigs systems, MethodInfo method)
	{
		var attrs = GetCustomAttributes(method);
		return ApplyAll(systems, attrs);
	}

	public static IIntoSystemConfigs ApplyAll(IIntoSystemConfigs systems, Attribute[] attributes)
	{
		var cfg = systems;
		foreach (var attr in attributes) {
			if (attr is SystemConfigAttribute configAttr)
				cfg = configAttr.Apply(cfg);
		}
		return cfg;
	}
}

public abstract class EnumSetReferenceAttribute<T> : SystemConfigAttribute where T : struct, Enum
{
	protected IIntoSystemSet ReferenceSet;

	public EnumSetReferenceAttribute(T set) => ReferenceSet = new EnumSet(set);
}

public abstract class GenericSetReferenceAttribute<T> : SystemConfigAttribute where T : ISystem
{
	protected IIntoSystemSet ReferenceSet;

	public GenericSetReferenceAttribute() => ReferenceSet = new SystemTypeSet<T>();
}

public class BeforeSystemAttribute<T> : GenericSetReferenceAttribute<T> where T : ISystem
{
	public override IIntoNodeConfigs<ISystem> Apply(IIntoNodeConfigs<ISystem> configs) => configs.Before(ReferenceSet);
}

public class BeforeAttribute<TEnum> : EnumSetReferenceAttribute<TEnum> where TEnum : struct, Enum
{
	public BeforeAttribute(TEnum set) : base(set) { }

	public override IIntoNodeConfigs<ISystem> Apply(IIntoNodeConfigs<ISystem> configs) => configs.Before(ReferenceSet);
}

public class AfterSystemAttribute<T> : GenericSetReferenceAttribute<T> where T : ISystem
{
	public override IIntoNodeConfigs<ISystem> Apply(IIntoNodeConfigs<ISystem> configs) => configs.After(ReferenceSet);
}

public class AfterAttribute<TEnum> : EnumSetReferenceAttribute<TEnum> where TEnum : struct, Enum
{
	public AfterAttribute(TEnum set) : base(set) { }

	public override IIntoNodeConfigs<ISystem> Apply(IIntoNodeConfigs<ISystem> configs) => configs.After(ReferenceSet);
}

public class InSetAttribute<T>(T set) : EnumSetReferenceAttribute<T>(set)
	where T : struct, Enum
{
	public override IIntoNodeConfigs<ISystem> Apply(IIntoNodeConfigs<ISystem> configs) => configs.InSet(ReferenceSet);
}