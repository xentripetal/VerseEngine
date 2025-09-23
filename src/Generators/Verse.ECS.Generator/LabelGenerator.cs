using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Verse.ECS.Generator;

[Generator]
public class LabelGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var labels = context.SyntaxProvider.CreateSyntaxProvider(((node, cancel) => node is TypeDeclarationSyntax t && t.AttributeLists.HasAttribute("Label")),
			PartialLabel.TryParseClass);

		// Report any diagnostic failures
		context.RegisterSourceOutput(labels.
				Where(x => x.Diagnostics.Count > 0).
				Select((x, _) => x.Diagnostics),
			Report);

		// Build any valid parses
		context.RegisterSourceOutput(labels.
				Where(x => x.HasValue).
				Select((v, _) => v.Value),
			Build);
	}


	private static void Report(SourceProductionContext ctx, List<Diagnostic> diagnostics)
	{
		foreach (var diagnostic in diagnostics) {
			ctx.ReportDiagnostic(diagnostic);
		}
	}

	private static void Build(SourceProductionContext ctx, PartialLabel builder)
	{
		var file = builder.Generate(ctx);
		ctx.AddSource(builder.GeneratedFileName, SourceText.From(CodeFormatter.Format(file), Encoding.UTF8));
	}


	public class PartialLabel()
	{
		public static ParseValue<PartialLabel> TryParseClass(GeneratorSyntaxContext ctx, CancellationToken cancel)
		{
			var syntax = ctx.Node as TypeDeclarationSyntax;
			if (syntax is null)
				return ParseValue<PartialLabel>.Empty();

			if (!syntax.IsPartial())
				return ParseValue<PartialLabel>.Err(Diagnostic.Create(Diagnostics.MissingPartial, syntax.GetLocation()));

			if (ctx.SemanticModel.GetDeclaredSymbol(syntax) is not { } typeSymbol)
				return ParseValue<PartialLabel>.Empty();
			if (!syntax.AttributeLists.TryGetAttribute("Label", out var attr)) {
				return ParseValue<PartialLabel>.Empty();
			}
			if (attr.Name is not GenericNameSyntax name) {
				return ParseValue<PartialLabel>.Empty();
			}
			if (name.TypeArgumentList.Arguments.Count != 1) {
				return ParseValue<PartialLabel>.Empty();
			}
			var label = new PartialLabel();
			label.Namespace = typeSymbol.ContainingNamespace.ToString();
			label.TypeName = typeSymbol.Name;
			label.LabelType = name.TypeArgumentList.Arguments[0].ToFullString();
			label.Syntax = syntax;

			return ParseValue<PartialLabel>.Ok(label);
		}

		public string GeneratedFileName => $"{Namespace}.{TypeName}.Label.g.cs";

		public string Namespace;
		public string TypeName;
		public string LabelType;
		public TypeDeclarationSyntax Syntax;

		public string Generate(SourceProductionContext ctx)
		{
			var sb = new StringBuilder();
			foreach (var use in Syntax.GetFileUsings()) {
				sb.AppendLine($"using {use.Name.ToString()};");
			}
			sb.AppendLine(GenHelpers.WrapPartial(Namespace, Syntax, $@"
	public partial {Syntax.Keyword.ToString()} {TypeName} : {LabelType} {{
		public bool Equals(Verse.ECS.ILabel other) => other is {TypeName};
		public override bool Equals(object? obj) => obj is {TypeName};
		public override int GetHashCode() => typeof({TypeName}).GetHashCode();
		public static {LabelType} Label() => new {TypeName}();
		public string GetLabelName() => nameof({TypeName});
	}}"));
			return sb.ToString();
		}
	}
}