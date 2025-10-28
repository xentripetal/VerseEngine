using Verse.ECS.Systems;

namespace Verse.ECS;

public interface ISystemParam
{
	/// <summary>
	/// Ran on system init
	/// </summary>
	/// <param name="system"></param>
	/// <param name="combinedAccess"></param>
	/// <param name="world"></param>
	public void Init(ISystem system, World world);
	/// <summary>
	/// Checks whether the Param is ready to be used in a system.
	/// Note this has direct world access but is running in a multi-threaded context and should not perform any dangerous operations.
	/// </summary>
	/// <remarks>Bevy uses a UnsafeWorldCell for safety here but we dont have that</remarks>
	/// <param name="world"></param>
	/// <exception cref="Exception">Any exceptions that would result in an invalid param</exception>
	public void ValidateParam(SystemMeta meta, World world, Tick thisRun);
}