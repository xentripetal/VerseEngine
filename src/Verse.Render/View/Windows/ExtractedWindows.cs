using Verse.MoonWorks;

namespace Verse.Render.View.Windows;

public class ExtractedWindows
{
	public ulong? PrimaryWindowEntity;
	public Dictionary<ulong, ExtractedWindow> Windows = new Dictionary<ulong, ExtractedWindow>();
}

public struct ExtractedWindow
{
	public ulong Entity;
	// TODO probably want to change this
	public Window Window;
}