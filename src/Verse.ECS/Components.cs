namespace Verse.ECS;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
internal sealed class ComponentComparer :
	IComparer<ulong>,
	IComparer<SlimComponent>
{
	public int Compare(SlimComponent x, SlimComponent y) => CompareTerms(x.Id, y.Id);

	public int Compare(ulong x, ulong y) => CompareTerms(x, y);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CompareTerms(ulong a, ulong b) => a.CompareTo(b);
}

[DebuggerDisplay("ID: {Id}, Size: {Size}")]
[SkipLocalsInit]
public readonly record struct SlimComponent(ulong Id, int Size)
{
	public bool IsTag => Size == 0;
}

public readonly record struct ComponentType
{
	private ComponentType(Type type, int size)
	{
		Type = type;
		Size = size;
	}

	private ComponentType(EcsID entityId)
	{
		Size = 0;
		EntityId = entityId;
	}

	public static ComponentType OfEntity(EcsID entityId) => new ComponentType(entityId);
	public static ComponentType OfCLRType(Type type, int size) => new ComponentType(type, size);

	public int Size { get; }
	public Type? Type { get; }
	public EcsID EntityId { get; }
	
	public bool IsCLR => Type != null;
	public bool IsEntityTag => EntityId != 0;
	
}

[SkipLocalsInit]
public class Component
{
	public Component(ComponentType type, string name, ulong id, World world, Func<int, Array?> creator)
	{
		Type = type;
		Name = name;
		Id = id;
		World = world;
		Size = type.Size;
		Slim = new SlimComponent(id, Size);
		Hooks = new RawComponentHooks();
		Creator = creator;
	}

	public readonly Func<int, Array?> Creator;
	public readonly ComponentType Type;
	public readonly string Name;
	public readonly ulong Id;
	public readonly World World;
	public RawComponentHooks Hooks;

	public readonly SlimComponent Slim;
	public readonly int Size;
	public bool IsTag => Size == 0;
	public bool IsValue => Type.IsCLR;
	public bool IsDynamic => Type.IsEntityTag;
}

public struct RawComponentHooks
{
	public unsafe delegate*<EntityView, void*, void> OnAdd;
	public unsafe delegate*<EntityView, void*, void> OnSet;
	public unsafe delegate*<EntityView, void*, void> OnRemove;
}

public interface IHookedComponent<T>
{
	public void OnAdd(EntityView view);
	public void OnSet(EntityView view);
	public void OnRemove(EntityView view);

	internal static unsafe void DynOnAdd(EntityView view, void* data)
	{
		var self = (IHookedComponent<T>*)data;
		self->OnAdd(view);
	}

	internal static unsafe void DynOnSet(EntityView view, void* data)
	{
		var self = (IHookedComponent<T>*)data;
		self->OnSet(view);
	}

	internal static unsafe void DynOnRemove(EntityView view, void* data)
	{
		var self = (IHookedComponent<T>*)data;
		self->OnRemove(view);
	}
}

public class EntityComponent(ulong id, EcsID entityId, World world) : Component(ComponentType.OfEntity(entityId), world.Entity(entityId).Name(), id, world,
	(_) => null);

/// <summary>
/// A static reference to a <see cref="Component"/> type.
/// </summary>
/// <typeparam name="T"></typeparam>
[SkipLocalsInit]
public class Component<T> : Component
{
	public Component(ulong id, World world) : base(ComponentType.OfCLRType(typeof(T), StaticSize), StaticName, id, world, CreateArray)
	{
		if (typeof(T).IsAssignableTo(typeof(IHookedComponent<T>))) {
			unsafe {
				Hooks.OnAdd = &IHookedComponent<T>.DynOnAdd;
				Hooks.OnSet = &IHookedComponent<T>.DynOnSet;
				Hooks.OnRemove = &IHookedComponent<T>.DynOnRemove;
			}
		}
	}

	// ReSharper disable once StaticMemberInGenericType
	public static readonly int StaticSize = GetSize();
	// ReSharper disable once StaticMemberInGenericType
	public static readonly string StaticName = typeof(T).FullName ?? typeof(T).Name;

	public static Array CreateArray(int count)
	{
		if (StaticSize == 0) {
			return Array.Empty<T>();
		}
		return new T[count];
	}


	private static int GetSize()
	{
		var size = RuntimeHelpers.IsReferenceOrContainsReferences<T>() ? IntPtr.Size : Unsafe.SizeOf<T>();

		if (size != 1)
			return size;

		// credit: BeanCheeseBurrito from Flecs.NET
		Unsafe.SkipInit<T>(out var t1);
		Unsafe.SkipInit<T>(out var t2);
		Unsafe.As<T, byte>(ref t1) = 0x7F;
		Unsafe.As<T, byte>(ref t2) = 0xFF;

		return Equals(t1, t2) ? 0 : size;
	}
}

public class ComponentRegistry(World world)
{
	private ulong _index;
	private readonly FastIdLookup<SlimComponent> _slimComponents = new FastIdLookup<SlimComponent>();
	private readonly FastIdLookup<Component> _components = new FastIdLookup<Component>();
	private readonly Dictionary<Type, EcsID> _typeToId = new Dictionary<Type, EcsID>();

	public EcsID ClaimKey()
	{
		return Interlocked.Increment(ref _index);
	}
	public EcsID RegisterComponent<T>() where T : struct
	{
		if (_typeToId.TryGetValue(typeof(T), out var id)) {
			return id;
		}
		var c = new Component<T>(ClaimKey(), world);
		_typeToId.Add(typeof(T), c.Id);
		_slimComponents.Add(c.Id, c.Slim);
		_components.Add(c.Id, c);
		return c.Id;
	}

	public ref readonly SlimComponent GetSlimComponent(EcsID id)
	{
		ref readonly var cmp = ref _slimComponents.TryGet(id, out var exists);
		if (!exists)
			EcsAssert.Panic(false, $"component not found with id {id}");
		return ref cmp;
	}

	public ref readonly SlimComponent GetSlimComponent<T>() where T : struct
	{
		if (_typeToId.TryGetValue(typeof(T), out var id)) {
			return ref _slimComponents[id];
		}
		id = RegisterComponent<T>();
		return ref _slimComponents[id];
	}

	public Component GetComponent(EcsID id)
	{
		ref readonly var cmp = ref _components.TryGet(id, out var exists);
		if (!exists)
			EcsAssert.Panic(false, $"component not found with hashcode {id}");
		return cmp;
	}

	public Component GetComponent<T>() where T : struct
	{
		if (_typeToId.TryGetValue(typeof(T), out var id)) {
			return _components[id];
		}
		id = RegisterComponent<T>();
		return _components[id];
	}

	public Array? GetArray(EcsID id, int count)
	{
		ref var c = ref _components.TryGet(id, out var exists);
		if (exists)
			return c.Creator(count);

		EcsAssert.Panic(false, $"component not found with id {id}");
		return null;
	}
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
