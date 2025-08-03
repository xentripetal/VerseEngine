namespace Verse.ECS;

public static class Defaults
{
	/// <summary>
	///     Wildcard is used to specify "any component/tag".<br />It's mostly used for queries.
	/// </summary>
	public readonly struct Wildcard
	{
		public static readonly EcsID ID = Lookup.Component<Wildcard>.Value.ID;
	}
}