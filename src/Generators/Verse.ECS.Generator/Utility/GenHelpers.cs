using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Verse.ECS.Generator;

public static class GenHelpers
{
	public static string GenerateSequence(int count, string separator, Func<int, string> generator)
	{
		var sb = new StringBuilder();
		for (var i = 0; i < count; ++i) {
			sb.Append(generator(i));

			if (i < count - 1) {
				sb.Append(separator);
			}
		}

		return sb.ToString();
	}

	/// <summary>
	/// WrapPartial will generate the nested structure of the given syntax.
	/// For example, if you have a class `Foo` with a nested class `Bar` and pass us Bar,
	/// we will generate the following:
	/// <example>
	/// <code>
	/// namespace MyNamespace {
	///		public partial class Foo {
	///			{body}
	///		}
	/// }
	/// </code>
	/// </example>
	/// </summary>
	/// <param name="ns"></param>
	/// <param name="syntax"></param>
	/// <param name="body"></param>
	/// <returns></returns>
	public static string WrapPartial(string ns, TypeDeclarationSyntax syntax, string body)
	{
		// If were a nested class, we need to replicate the parent structure
		Stack<TypeDeclarationSyntax> parents = new Stack<TypeDeclarationSyntax>();
		var rootNode = syntax;
		while (rootNode.Parent is TypeDeclarationSyntax parent) {
			rootNode = parent;
			parents.Push(rootNode);
		}
		var sb = new StringBuilder();


		sb.AppendLine($"namespace {ns} {{");
		foreach (var parent in parents) {
			sb.AppendLine($"	public partial {parent.Keyword.ToString()} {parent.Identifier.ToString()} {{");
		}
		sb.AppendLine(body);
		foreach (var _ in parents) {
			sb.AppendLine("}");
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}