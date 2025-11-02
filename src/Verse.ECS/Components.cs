namespace Verse.ECS;

/// <summary>
/// A value which uniquely identifies the type of a <see cref="Component"/> or Resource within a <see cref="World"/>
/// </summary>
/// <param name="Id"></param>
public readonly record struct ComponentId(uint Id) : ISparseSetIndex<ComponentId>
{
	public ComponentId(ulong id) : this((uint)id) { }
	public int SparseSetIndex() => (int)Id;
	public static ComponentId GetSparseSetIndex(int index) => new ComponentId((uint)index);

	public static implicit operator uint(ComponentId id) => id.Id;
	public static implicit operator ComponentId(uint id) => new ComponentId(id);
	public int CompareTo(ComponentId id)
	{
		return Id.CompareTo(id.Id);
	}
}

public enum StorageType
{
	/// <summary>
	/// Provides fast and cache-friendly iteration, but slower addition and removal of components. This is the default
	/// storage type
	/// </summary>
	Table,
	/// <summary>
	/// Provides fast addition and removal of components, but slower iteration
	/// </summary>
	SparseSet
}


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
public readonly record struct SlimComponent(ComponentId Id, int Size)
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
	public Component(ComponentType type, string name, ComponentId id, World world, Func<int, Array?> creator)
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
	public readonly ComponentId Id;
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

/// <summary>
/// A static reference to a <see cref="Component"/> type.
/// </summary>
/// <typeparam name="T"></typeparam>
[SkipLocalsInit]
public class Component<T> : Component
{
	public Component(ComponentId id, World world) : base(ComponentType.OfCLRType(typeof(T), StaticSize), StaticName, id, world, CreateArray)
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
	public static readonly string StaticName = typeof(T).ToString();

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
	private uint _index;
	private readonly FastIdLookup<Component> components = new FastIdLookup<Component>();
	private readonly Dictionary<Type, ComponentId> indices = new Dictionary<Type, ComponentId>();
	private readonly Dictionary<Type, ComponentId> resourceIndices = new Dictionary<Type, ComponentId>();

	public ComponentId ClaimKey()
	{
		return Interlocked.Increment(ref _index);
	}
	public ComponentId RegisterComponent<T>() 
	{
		if (indices.TryGetValue(typeof(T), out var id)) {
			return id;
		}
		var c = new Component<T>(ClaimKey(), world);
		indices.Add(typeof(T), c.Id);
		components.Add(c.Id, c);
		return c.Id;
	}

	public ref readonly SlimComponent GetSlimComponent(EcsID id)
	{
		ref readonly var cmp = ref components.TryGet(id, out var exists);
		if (!exists)
			EcsAssert.Panic(false, $"component not found with id {id}");
		return ref cmp.Slim;
	}

	public ref readonly SlimComponent GetSlimComponent<T>() 
	{
		if (indices.TryGetValue(typeof(T), out var id)) {
			return ref components[id].Slim;
		}
		id = RegisterComponent<T>();
		return ref components[id].Slim;
	}

	public ComponentId? ComponentId<T>() where T : struct
	{
		if (indices.TryGetValue(typeof(T), out var id)) {
			return id;
		}
		return null;
	}
	

	public Component GetComponent(EcsID id)
	{
		ref readonly var cmp = ref components.TryGet(id, out var exists);
		if (!exists)
			EcsAssert.Panic(false, $"component not found with hashcode {id}");
		return cmp;
	}

	public Component GetComponent<T>() where T : struct
	{
		if (indices.TryGetValue(typeof(T), out var id)) {
			return components[id];
		}
		id = RegisterComponent<T>();
		return components[id];
	}

	public ComponentId RegisterResource<T>()
	{
		if (resourceIndices.TryGetValue(typeof(T), out var id)) {
			return id;
		}
		var c = new Component<T>(ClaimKey(), world);
		resourceIndices.Add(typeof(T), c.Id);
		components.Add(c.Id, c);
		return c.Id;	
	}

	public ComponentId? ResourceId<T>()
	{
		if (resourceIndices.TryGetValue(typeof(T), out var id)) {
			return id;
		}
		return null;
	}

	public Array? GetArray(EcsID id, int count)
	{
		ref var c = ref components.TryGet(id, out var exists);
		if (exists)
			return c.Creator(count);

		EcsAssert.Panic(false, $"component not found with id {id}");
		return null;
	}
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type