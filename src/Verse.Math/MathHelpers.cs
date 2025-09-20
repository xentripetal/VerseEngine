namespace Verse.Math;

public class MathHelper
{
	public static float ToDegrees(float radians) => radians * 57.29578f;

	public static float ToRadians(float degrees) => degrees * PiOver180;

	/// <summary>
	///     Represents the log base ten of e(0.4342945).
	/// </summary>
	public const float Log10E = 0.4342945f;

	/// <summary>
	///     Represents the log base two of e(1.442695).
	/// </summary>
	public const float Log2E = 1.442695f;

	/// <summary>
	///     Represents the value of pi divided by two(1.57079637).
	/// </summary>
	public const float PiOver2 = float.Pi / 2f;

	/// <summary>
	///     Represents the value of pi divided by four(0.7853982).
	/// </summary>
	public const float PiOver4 = float.Pi / 4f;

	public const float PiOver180 = float.Pi / 180f;

	/// <summary>
	///     Represents the value of pi times two(6.28318548).
	/// </summary>
	public const float TwoPi = float.Pi * 2f;

	internal static readonly float MachineEpsilonFloat = GetMachineEpsilonFloat();

	/// <summary>
	///     Returns the Cartesian coordinate for one axis of a point that is defined by a
	///     given triangle and two normalized barycentric (areal) coordinates.
	/// </summary>
	/// <param name="value1">
	///     The coordinate on one axis of vertex 1 of the defining triangle.
	/// </param>
	/// <param name="value2">
	///     The coordinate on the same axis of vertex 2 of the defining triangle.
	/// </param>
	/// <param name="value3">
	///     The coordinate on the same axis of vertex 3 of the defining triangle.
	/// </param>
	/// <param name="amount1">
	///     The normalized barycentric (areal) coordinate b2, equal to the weighting factor
	///     for vertex 2, the coordinate of which is specified in value2.
	/// </param>
	/// <param name="amount2">
	///     The normalized barycentric (areal) coordinate b3, equal to the weighting factor
	///     for vertex 3, the coordinate of which is specified in value3.
	/// </param>
	/// <returns>
	///     Cartesian coordinate of the specified point with respect to the axis being used.
	/// </returns>
	public static float Barycentric(
		float value1,
		float value2,
		float value3,
		float amount1,
		float amount2
	) => value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;

	/// <summary>
	///     Performs a Catmull-Rom interpolation using the specified positions.
	/// </summary>
	/// <param name="value1">The first position in the interpolation.</param>
	/// <param name="value2">The second position in the interpolation.</param>
	/// <param name="value3">The third position in the interpolation.</param>
	/// <param name="value4">The fourth position in the interpolation.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>A position that is the result of the Catmull-Rom interpolation.</returns>
	public static float CatmullRom(
		float value1,
		float value2,
		float value3,
		float value4,
		float amount
	)
	{
		/* Using formula from http://www.mvps.org/directx/articles/catmull/
		 * Internally using doubles not to lose precision.
		 */
		double amountSquared = amount * amount;
		var amountCubed = amountSquared * amount;
		return (float)(
			0.5 *
			(
			2.0 * value2 + (value3 - value1) * amount +
			(2.0 * value1 - 5.0 * value2 + 4.0 * value3 - value4) * amountSquared +
			(3.0 * value2 - value1 - 3.0 * value3 + value4) * amountCubed
			)
			);
	}

	/// <summary>
	///     Performs a Hermite spline interpolation.
	/// </summary>
	/// <param name="value1">Source position.</param>
	/// <param name="tangent1">Source tangent.</param>
	/// <param name="value2">Source position.</param>
	/// <param name="tangent2">Source tangent.</param>
	/// <param name="amount">Weighting factor.</param>
	/// <returns>The result of the Hermite spline interpolation.</returns>
	public static float Hermite(
		float value1,
		float tangent1,
		float value2,
		float tangent2,
		float amount
	)
	{
		/* All transformed to double not to lose precision
		 * Otherwise, for high numbers of param:amount the result is NaN instead
		 * of Infinity.
		 */
		double v1 = value1, v2 = value2, t1 = tangent1, t2 = tangent2, s = amount;
		double result;
		var sCubed = s * s * s;
		var sSquared = s * s;

		if (WithinEpsilon(amount, 0f)) {
			result = value1;
		} else if (WithinEpsilon(amount, 1f)) {
			result = value2;
		} else {
			result = (2 * v1 - 2 * v2 + t2 + t1) * sCubed +
			         (3 * v2 - 3 * v1 - 2 * t1 - t2) * sSquared +
			         t1 * s +
			         v1;
		}

		return (float)result;
	}

	public static float Quantize(float value, float step) => MathF.Floor(value / step) * step;

	public static double Quantize(double value, double step) => double.Floor(value / step) * step;

	/// <summary>
	///     Rescales a value within a given range to a new range.
	/// </summary>
	public static float Normalize(short value, short min, short max, short newMin, short newMax) =>
		(float)(value - min) * (newMax - newMin) / (max - min) + newMin;

	/// <summary>
	///     Rescales a value within a given range to a new range.
	/// </summary>
	public static float Normalize(float value, float min, float max, float newMin, float newMax) => (value - min) * (newMax - newMin) / (max - min) + newMin;

	/// <summary>
	///     Step from start towards end by change.
	/// </summary>
	/// <param name="start">Start value.</param>
	/// <param name="end">End value.</param>
	/// <param name="change">Change value.</param>
	public static float Approach(float start, float end, float change) =>
		start < end ? System.Math.Min(start + change, end) : System.Math.Max(start - change, end);

	/// <summary>
	///     Step from start towards end by change.
	/// </summary>
	/// <param name="start">Start value.</param>
	/// <param name="end">End value.</param>
	/// <param name="change">Change value.</param>
	public static int Approach(int start, int end, int change) => start < end ? System.Math.Min(start + change, end) : System.Math.Max(start - change, end);

	/// <summary>
	///     Step from start towards end by change.
	/// </summary>
	/// <param name="start">Start value.</param>
	/// <param name="end">End value.</param>
	/// <param name="change">Change value.</param>
	public static double Approach(double start, double end, double change) => start < end ? double.Min(start + change, end) : double.Max(start - change, end);

	internal static bool WithinEpsilon(float floatA, float floatB) => System.Math.Abs(floatA - floatB) < MachineEpsilonFloat;

	/// <summary>
	///     Find the current machine's Epsilon for the float data type.
	///     (That is, the largest float, e,  where e == 0.0f is true.)
	/// </summary>
	private static float GetMachineEpsilonFloat()
	{
		var machineEpsilon = 1.0f;
		float comparison;

		/* Keep halving the working value of machineEpsilon until we get a number that
		 * when added to 1.0f will still evaluate as equal to 1.0f.
		 */
		do {
			machineEpsilon *= 0.5f;
			comparison = 1.0f + machineEpsilon;
		}
		while (comparison > 1.0f);

		return machineEpsilon;
	}
}