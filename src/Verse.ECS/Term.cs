using System.Diagnostics.CodeAnalysis;

namespace Verse.ECS;

public interface IQueryTerm : IComparable<IQueryTerm>
{
	ComponentId Id { get; init; }
	TermOp Op { get; init; }
	TermAccess Access { get; init; }

	int IComparable<IQueryTerm>.CompareTo([NotNull] IQueryTerm? other)
	{
		var res = Id.CompareTo(other!.Id);
		if (res != 0)
			return res;
		var op = Op.CompareTo(other.Op);
		if (op != 0)
			return op;
		return Access.CompareTo(other.Access);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	ArchetypeSearchResult Match(Archetype archetype);
}

[DebuggerDisplay("{Id} - {Op}")]
public readonly struct WithTerm(ComponentId id, TermAccess Access = TermAccess.Read) : IQueryTerm
{
	public ComponentId Id { get; init; } = id;
	public TermOp Op { get; init; } = TermOp.With;
	public TermAccess Access { get; init; } = Access;

	public readonly ArchetypeSearchResult Match(Archetype archetype) => archetype.HasIndex(Id) ? ArchetypeSearchResult.Found : ArchetypeSearchResult.Continue;
}


[DebuggerDisplay("{Id} - {Op}")]
public readonly struct WithoutTerm(ComponentId id) : IQueryTerm
{
	public ComponentId Id { get; init; } = id;
	public TermOp Op { get; init; } = TermOp.Without;
	public TermAccess Access { get; init; } = TermAccess.None;

	public readonly ArchetypeSearchResult Match(Archetype archetype) => archetype.HasIndex(Id) ? ArchetypeSearchResult.Stop : ArchetypeSearchResult.Continue;
}

[DebuggerDisplay("{Id} - {Op}")]
public readonly struct OptionalTerm(ComponentId id) : IQueryTerm
{
	public ComponentId Id { get; init; } = id;
	public TermOp Op { get; init; } = TermOp.Optional;
	public TermAccess Access { get; init; } = TermAccess.Write;

	public readonly ArchetypeSearchResult Match(Archetype archetype) => ArchetypeSearchResult.Found;
}

[DebuggerDisplay("{Id} - {Op}")]
public readonly struct OptionalROTerm(ComponentId id) : IQueryTerm
{
	public ComponentId Id { get; init; } = id;
	public TermOp Op { get; init; } = TermOp.Optional;
	public TermAccess Access { get; init; } = TermAccess.Read;

	public readonly ArchetypeSearchResult Match(Archetype archetype) => ArchetypeSearchResult.Found;
}

public enum TermOp : byte
{
	With,
	Without,
	Optional
}

public enum TermAccess : byte
{
	None,
	Read,
	Write,
}