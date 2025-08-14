using SDL3;
using Verse.MoonWorks.Math;

namespace Verse.MoonWorks.Input;

/// <summary>
///     Represents a trigger input on a gamepad.
/// </summary>
public class Trigger
{
	public SDL.SDL_GamepadAxis SDL_Axis;

	public Trigger(
		Gamepad parent,
		TriggerCode code,
		SDL.SDL_GamepadAxis sdlAxis
	)
	{
		Parent = parent;
		Code = code;
		SDL_Axis = sdlAxis;
	}
	public Gamepad Parent { get; }

	public TriggerCode Code { get; }

	/// <summary>
	///     A trigger value between 0 and 1.
	/// </summary>
	public float Value { get; private set; }

	internal void Update()
	{
		Value = MathHelper.Normalize(
			SDL.SDL_GetGamepadAxis(Parent.Handle, SDL_Axis),
			0, short.MaxValue,
			0, 1
		);
	}
}