using System.Numerics;
using Verse.Math;

namespace Verse.Camera;

public enum ScalingModeType
{
	WindowSize,
	Fixed,
	AutoMin,
	AutoMax,
	FixedVertical,
	FixedHorizontal
}

/// <summary>
/// Scaling mode for <see cref="OrthographicProjection"/>. The effects of these modes are combined with the Scale property
/// </summary>
/// <example>
/// If the scaling mode is `ScalingMode::Fixed { width: 100.0, height: 300 }` and the scale is `2.0`,
/// the projection will be 200 world units wide and 600 world units tall.
/// </example>
public record struct ScalingMode
{
	public ScalingMode()
	{
		Type = ScalingModeType.WindowSize;
	}

	public ScalingMode(ScalingModeType type, float x = 0, float y = 0)
	{
		Type = type;
		X = x;
		Y = y;
	}

	public ScalingModeType Type;
	public float X, Y;

	public static ScalingMode WindowSize() => new ScalingMode { Type = ScalingModeType.WindowSize };
	public bool IsWindowSize() => Type == ScalingModeType.WindowSize;

	public static ScalingMode Fixed(float x, float y) => new ScalingMode { Type = ScalingModeType.Fixed, X = x, Y = y };
	public bool IsFixed() => Type == ScalingModeType.Fixed;
	public static ScalingMode AutoMin(float minWidth, float minHeight) => new ScalingMode { Type = ScalingModeType.AutoMin, X = minWidth, Y = minHeight };
	public bool IsAutoMin() => Type == ScalingModeType.AutoMin;
	public static ScalingMode AutoMax(float maxWidth, float maxHeight) => new ScalingMode { Type = ScalingModeType.AutoMax, X = maxWidth, Y = maxHeight };
	public bool IsAutoMax() => Type == ScalingModeType.AutoMax;
	public static ScalingMode FixedVertical(float y) => new ScalingMode { Type = ScalingModeType.FixedVertical, Y = y };
	public bool IsFixedVertical() => Type == ScalingModeType.FixedVertical;
	public static ScalingMode FixedHorizontal(float x) => new ScalingMode { Type = ScalingModeType.FixedHorizontal, X = x };
	public bool IsFixedHorizontal() => Type == ScalingModeType.FixedHorizontal;
}

public struct OrthographicProjection()
{

	/// <summary>
	/// The distance of the near clipping plane in world units
	/// </summary>
	public float Near = 0;

	/// <summary>
	/// The distance of the far clipping plane in world units
	/// </summary>
	public float Far = 1000;

	public Vector2 ViewportOrigin = new Vector2(0.5f, 0.5f);
	/// <summary>
	/// How the projection will scale to the viewport.
	/// </summary>
	/// <remarks>
	/// <p> Defaults to <see cref="ScalingModeType.WindowSize"/> and works in concert with <see cref="Scale"/> to determine the final effect. </p>
	/// For simplicity, zooming should be done by changing [`OrthographicProjection::scale`],
	/// rather than changing the parameters of the scaling mode.
	///</remarks>
	public ScalingMode ScalingMode = ScalingMode.WindowSize();
	/// <summary>
	/// Scales the projection. As scale increases, the apparent size of objects decreases, and vice-versa.
	/// </summary>
	public float Scale = 1;

	/// <summary>
	/// The area that the projection covers relative to <see cref="ViewportOrigin"/>.
	/// </summary>
	/// <remarks>
	/// This should generally be controlled via <see cref="Update"/> which is automatically called by the camera system.
	/// </remarks>
	public Rect Area = default;

	public void Update(float width, float height)
	{
		var (projectionWidth, projectionHeight) = ScalingMode.Type switch {
			ScalingModeType.WindowSize => (width, height),
			ScalingModeType.Fixed      => (ScalingMode.X, ScalingMode.Y),
			// compare pixels fo current width and minimal height and pixels of minimal width with current height. Then use the bigger
			ScalingModeType.AutoMin => (width * ScalingMode.X > ScalingMode.X * height)
				? (width * ScalingMode.Y / height, ScalingMode.Y)
				: (ScalingMode.X, height * ScalingMode.X / width),
			ScalingModeType.AutoMax => (width * ScalingMode.Y < ScalingMode.X * height)
				? (width * ScalingMode.Y / height, ScalingMode.Y)
				: (ScalingMode.X, height * ScalingMode.X / width),
			ScalingModeType.FixedVertical   => (width * ScalingMode.Y / height, ScalingMode.Y),
			ScalingModeType.FixedHorizontal => (ScalingMode.X, height * ScalingMode.X / width),
			_                               => throw new ArgumentOutOfRangeException()
		};

		var originX = projectionWidth * ViewportOrigin.X;
		var originY = projectionHeight * ViewportOrigin.Y;
		Area = new Rect(Scale * -originX, Scale * -originY, Scale * (projectionWidth- originX), Scale * (projectionHeight - originY));
	}
}