using Verse.MoonWorks.lib.SDL3_CS.SDL3;
using Verse.MoonWorks.Math;

namespace Verse.MoonWorks.Input;

/// <summary>
///     Represents a specific joystick direction on a gamepad.
/// </summary>
public class Axis
{
	private readonly SDL.SDL_GamepadAxis SDL_Axis;

	public Axis(
		Gamepad parent,
		AxisCode code,
		SDL.SDL_GamepadAxis sdlAxis
	)
	{
		Parent = parent;
		SDL_Axis = sdlAxis;
		Code = code;
	}
	public Gamepad Parent { get; }

	public AxisCode Code { get; private set; }

	/// <summary>
	///     An axis value between -1 and 1.
	/// </summary>
	public float Value { get; private set; }

	internal void Update()
	{
		Value = MathHelper.Normalize(
			SDL.SDL_GetGamepadAxis(Parent.Handle, SDL_Axis),
			short.MinValue, short.MaxValue,
			-1, 1
		);
	}
}