using Verse.ECS;
using Verse.MoonWorks.Graphics;

namespace Verse.Render.Camera;

/// <summary>
/// For a <see cref="Camera"/>, specifies the color used to clear the viewport before rendering. or when writing to the final render target texture.
/// </summary>
public struct ClearColorConfig : IDefault<ClearColorConfig>
{
	public static ClearColorConfig Default() => new ClearColorConfig { };
	public static ClearColorConfig Custom(Color color) => new ClearColorConfig {
		CustomColor = color,
	};
	public static ClearColorConfig None() => new ClearColorConfig { NoClear = true };

	/// <summary>
	/// If true, no clear color is used, the camera will simply draw on top of anything already in the viewport.
	/// </summary>
	public bool NoClear;
	/// <summary>
	/// An optional custom clear color. If not present and <see cref="NoClear"/> is false, the default clear color will be used.
	/// </summary>
	public Color? CustomColor;
}

/// <summary>
/// A Resource that stores the default color that cameras use to clear the screen between frames.
///
/// This color appears as the background color for simple apps, when there are portions of the screen with nothing rendered.
/// </summary>
/// <remarks>Individual cameras may use <see cref="ClearColorConfig."/></remarks>
/// <param name="color"></param>
public struct ClearColor(Color color) : IDefault<ClearColor>
{
	public static ClearColor Default() => new ClearColor(Color.DarkSlateGray);
}