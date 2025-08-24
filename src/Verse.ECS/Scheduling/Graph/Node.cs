namespace Verse.ECS.Scheduling.Graph;

public enum NodeType
{
	System,
	Set
}

[DebuggerDisplay("{Name} ({Id}, {Type})")]
public struct NodeId : IEquatable<NodeId>
{
	public NodeId(int id, NodeType type, SystemGraph? graph = null)
	{
		Id = id;
		Type = type;
		Graph = graph;
	}

	public int Id;
	public NodeType Type;
	private SystemGraph? Graph;
	private string? Name => Graph?.GetNodeName(this);

	public bool IsSystem => Type == NodeType.System;
	public bool IsSet => Type == NodeType.Set;

	public bool Equals(NodeId other) => Id == other.Id && Type == other.Type;

	public override bool Equals(object? obj) => obj is NodeId other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Id, (int)Type);
}

public enum DependencyKind
{
	/// <summary>A node that should be preceded.</summary>
	Before,
	/// <summary>A node that should be succeeded.</summary>
	After,
	/// <summary>
	///     A node that should be preceded and will **not** automatically insert an instance of `apply_deferred` on the
	///     edge.
	/// </summary>
	BeforeNoSync,
	/// <summary>
	///     A node that should be succeeded and will **not** automatically insert an instance of `apply_deferred` on the
	///     edge.
	/// </summary>
	AfterNoSync
}