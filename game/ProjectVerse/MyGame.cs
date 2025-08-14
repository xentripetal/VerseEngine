using Verse.MoonWorks;
using Verse.MoonWorks.Graphics;

namespace ProjectVerse;

public class MyGame : Game
{

	public MyGame(AppInfo appInfo, WindowCreateInfo windowCreateInfo, FramePacingSettings framePacingSettings, ShaderFormat availableShaderFormats, bool debugMode = false) : base(appInfo, windowCreateInfo, framePacingSettings, availableShaderFormats,
		debugMode) { }

	protected override void Update(double delta) { }
	protected override void Draw(double delta, double alpha) { }
}