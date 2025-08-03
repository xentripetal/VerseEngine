using Verse.MoonWorks.lib.SDL3_CS.SDL3;

namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A virtual button corresponding to a mouse button.
/// </summary>
public class MouseButton : VirtualButton
{
	private readonly SDL.SDL_MouseButtonFlags ButtonMask;
	private readonly Mouse Parent;

	internal MouseButton(Mouse parent, MouseButtonCode code, SDL.SDL_MouseButtonFlags buttonMask)
	{
		Parent = parent;
		Code = code;
		ButtonMask = buttonMask;
	}

	public MouseButtonCode Code { get; private set; }

	internal override bool CheckPressed() => (Parent.ButtonMask & ButtonMask) != 0;
}