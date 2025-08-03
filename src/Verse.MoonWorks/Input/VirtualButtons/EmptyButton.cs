namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A dummy button that can never be pressed. Used for the dummy gamepad.
/// </summary>
public class EmptyButton : VirtualButton
{
	public static readonly EmptyButton Empty = new EmptyButton();

	internal override bool CheckPressed() => false;
}