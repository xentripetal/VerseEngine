using SDL3;

namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A virtual button corresponding to a gamepad button.
/// </summary>
public class GamepadButton : VirtualButton
{
	private readonly SDL.SDL_GamepadButton SDL_Button;

	internal GamepadButton(Gamepad parent, GamepadButtonCode code, SDL.SDL_GamepadButton sdlButton)
	{
		Parent = parent;
		Code = code;
		SDL_Button = sdlButton;
	}
	public Gamepad Parent { get; }
	public GamepadButtonCode Code { get; }

	internal override bool CheckPressed() => SDL.SDL_GetGamepadButton(Parent.Handle, SDL_Button);
}