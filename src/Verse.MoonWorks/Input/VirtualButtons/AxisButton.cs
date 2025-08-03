namespace Verse.MoonWorks.Input.VirtualButtons;

/// <summary>
///     A virtual button corresponding to a direction on a joystick.
///     If the axis value exceeds the threshold, it will be treated as a press.
/// </summary>
public class AxisButton : VirtualButton
{

	private readonly int Sign;

	private float threshold = 0.5f;

	internal AxisButton(Axis parent, bool positive)
	{
		Parent = parent;
		Sign = positive ? 1 : -1;

		if (parent.Code == AxisCode.LeftX) {
			if (positive) {
				Code = AxisButtonCode.LeftX_Right;
			} else {
				Code = AxisButtonCode.LeftX_Left;
			}
		} else if (parent.Code == AxisCode.LeftY) {
			if (positive) {
				Code = AxisButtonCode.LeftY_Down;
			} else {
				Code = AxisButtonCode.LeftY_Up;
			}
		} else if (parent.Code == AxisCode.RightX) {
			if (positive) {
				Code = AxisButtonCode.RightX_Right;
			} else {
				Code = AxisButtonCode.RightX_Left;
			}
		} else if (parent.Code == AxisCode.RightY) {
			if (positive) {
				Code = AxisButtonCode.RightY_Down;
			} else {
				Code = AxisButtonCode.RightY_Up;
			}
		}
	}
	public Axis Parent { get; }
	public AxisButtonCode Code { get; }
	public float Threshold {
		get => threshold;
		set => threshold = System.Math.Clamp(value, 0, 1);
	}

	internal override bool CheckPressed() => Sign * Parent.Value >= threshold;
}