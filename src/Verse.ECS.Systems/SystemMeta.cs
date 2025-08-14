namespace PolyECS.Systems;

public class SystemMeta
{
    public bool HasDeferred;
    public bool IsExclusive;
    public string Name;
    public FilteredAccessSet Access;

    public SystemMeta(string name)
    {
        Name = name;
        Access = new FilteredAccessSet();
        HasDeferred = true; // Due to the differences in Flecs vs bevy, we can't really determine if a system will push
                            // deferred changes or not. So we default to true and only disable it when we're certain.
    }
}
