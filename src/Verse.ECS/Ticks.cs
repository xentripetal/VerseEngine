namespace Verse.ECS;

public sealed partial class SystemTicks
{
	public uint LastRun { get; set; }
	public uint ThisRun { get; set; }
}