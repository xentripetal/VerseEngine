namespace Verse.ECS.Systems;

public class SystemMeta
{
    public bool HasDeferred;
    public bool IsExclusive;
    public string Name;
    public FilteredAccessSet Access;
    public SystemTicks Ticks;

    public SystemMeta(string name)
    {
        Name = name;
        Access = new FilteredAccessSet();
        Ticks = new SystemTicks();
        HasDeferred = false; 
    }
}
