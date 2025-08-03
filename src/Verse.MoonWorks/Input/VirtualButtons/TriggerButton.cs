namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A virtual button corresponding to a trigger on a gamepad.
///     If the trigger value exceeds the threshold, it will be treated as a press.
/// </summary>
public class TriggerButton : VirtualButton
{

	private float threshold = 0.7f;

	internal TriggerButton(Trigger parent)
	{
		Parent = parent;
	}
	public Trigger Parent { get; }
	public TriggerCode Code => Parent.Code;
	public float Threshold {
		get => threshold;
		set => threshold = System.Math.Clamp(value, 0, 1);
	}

	internal override bool CheckPressed() => Parent.Value >= Threshold;
}