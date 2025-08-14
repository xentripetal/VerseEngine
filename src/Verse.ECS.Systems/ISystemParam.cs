using Verse.ECS;

namespace PolyECS.Systems;

public interface ISystemParam<T>
{
	/// <summary>
	/// Ran on system init
	/// </summary>
	/// <param name="system"></param>
	/// <param name="combinedAccess"></param>
	/// <param name="world"></param>
	public void Init(ISystem system, FilteredAccessSet combinedAccess, World world);
	/// <summary>
	/// Checks whether the Param is ready to be used in a system.
	/// Note this has direct world access but is running in a multi-threaded context and should not perform any dangerous operations.
	/// </summary>
	/// <remarks>Bevy uses a UnsafeWorldCell for safety here but we dont have that</remarks>
	/// <param name="world"></param>
	/// <returns></returns>
	public bool Ready(World world);
}
