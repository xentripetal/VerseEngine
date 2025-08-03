namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A virtual button corresponding to a keyboard button.
/// </summary>
public class KeyboardButton : VirtualButton
{
	private readonly Keyboard Parent;

	internal KeyboardButton(Keyboard parent, KeyCode keyCode)
	{
		Parent = parent;
		KeyCode = keyCode;
	}
	public KeyCode KeyCode { get; }

	internal override unsafe bool CheckPressed() => Conversions.ByteToBool(((byte*)Parent.State)[(int)KeyCode]);
}