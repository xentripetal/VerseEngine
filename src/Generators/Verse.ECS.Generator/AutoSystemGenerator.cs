using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Verse.ECS.Generator;

[Generator]
public class AutoSystemGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var autoSystems = SyntaxProvider(context);

		// Report any diagnostic failures
		context.RegisterSourceOutput(autoSystems.
				Where(x => x.Diagnostics.Count > 0).
				Select((x, _) => x.Diagnostics),
			Report);

		// Build any valid parses
		context.RegisterSourceOutput(autoSystems.
				Where(x => x.HasValue).
				Select((v, _) => v.Value),
			Build);
	}

	private static IncrementalValuesProvider<ParseValue<AutoSystemContainer>> SyntaxProvider(IncrementalGeneratorInitializationContext context)
	{
		return context.SyntaxProvider.CreateSyntaxProvider(FilterCandidateSyntax, AutoSystemContainer.TryParseClass);
	}
	private static bool FilterCandidateSyntax(SyntaxNode node, CancellationToken cancel)
	{
		if (node is not ClassDeclarationSyntax c)
			return false;
		
		if (c.BaseList == null || !c.BaseList.Types.Select(x => x.Type.ToString().Equals("SystemContainer")).Any())
			return false;

		// has a method with [Schedule] attribute
		foreach (var mem in c.Members) {
			if (mem is not MethodDeclarationSyntax m) {
				continue;
			}
			foreach (var attrs in m.AttributeLists) {
				foreach (var attr in attrs.Attributes) {
					if (attr.Name.ToString() == "Schedule" || attr.Name.ToString() == "ScheduleAttribute") {
						return true;
					}
				}
			}
		}
		return false;
	}


	private static void Report(SourceProductionContext ctx, List<Diagnostic> diagnostics)
	{
		foreach (var diagnostic in diagnostics) {
			ctx.ReportDiagnostic(diagnostic);
		}
	}

	private static void Build(SourceProductionContext ctx, AutoSystemContainer builder)
	{
		var file = builder.Generate(ctx);
		var path = $"{builder.Namespace}.{builder.ClassName}.g.cs";
		ctx.AddSource(path, SourceText.From(CodeFormatter.Format(file), Encoding.UTF8));
	}
}