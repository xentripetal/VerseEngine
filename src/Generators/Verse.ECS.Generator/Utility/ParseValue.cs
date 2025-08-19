#nullable enable
using Microsoft.CodeAnalysis;

namespace Verse.ECS.Generator;

public struct ParseValue<T>
{
	public List<Diagnostic> Diagnostics = new();
	public T Value;
	public bool Set;
	public ParseValue()
	{
		Value = default;
		Set = false;
	}
	public bool HasValue => Set;
	
	public static ParseValue<T> Ok(T value) => new ParseValue<T> { Value = value, Set = true };
	public static ParseValue<T> Empty() => new ParseValue<T> { Diagnostics = new List<Diagnostic>() };
	public static ParseValue<T> Err(Diagnostic diagnostic) => new ParseValue<T> { Diagnostics = { diagnostic } };
}