using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Verse.ECS.Generator;

[Generator]
public class SchedulableGenerator : IIncrementalGenerator
{
	// TODO: clean this up and reuse behaviors with SystemContainerGenerator or drop SystemContainer
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

	private static IncrementalValuesProvider<ParseValue<Schedulable>> SyntaxProvider(IncrementalGeneratorInitializationContext context)
	{
		return context.SyntaxProvider.CreateSyntaxProvider(FilterCandidateSyntax, Schedulable.TryParseClass);
	}
	private static bool FilterCandidateSyntax(SyntaxNode node, CancellationToken cancel)
	{
		if (node is not ClassDeclarationSyntax c)
			return false;

		// Use SystemContainerGenerator for these types
		if (c.BaseList != null && c.BaseList.Types.Select(x => x.Type.ToString().Equals("SystemContainer")).Any())
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

	private static void Build(SourceProductionContext ctx, Schedulable builder)
	{
		var file = builder.Generate(ctx);
		var path = $"{builder.Namespace}.{builder.ClassName}.g.cs";
		ctx.AddSource(path, SourceText.From(CodeFormatter.Format(file), Encoding.UTF8));
	}


	public class Schedulable
	{
		public static ParseValue<Schedulable> TryParseClass(GeneratorSyntaxContext ctx, CancellationToken cancel)
		{
			var syntax = ctx.Node as TypeDeclarationSyntax;
			if (syntax is null)
				return ParseValue<Schedulable>.Empty();

			if (ctx.SemanticModel.GetDeclaredSymbol(syntax) is not { } classSymbol)
				return ParseValue<Schedulable>.Empty();

			if (!syntax.IsPartial())
				return ParseValue<Schedulable>.Err(Diagnostic.Create(Diagnostics.MissingPartial, syntax.GetLocation()));

			// TODO what about non scheduled containers?
			var systems = syntax.MethodsWithAttribute("Schedule", cancel);
			if (systems.Count == 0)
				return ParseValue<Schedulable>.Err(Diagnostic.Create(Diagnostics.MissingSystems, syntax.GetLocation()));

			var parsedSystems = new List<AutoSystemFunc>();
			foreach (var system in systems) {
				var parsed = AutoSystemFunc.TryParse(ctx, system, cancel);
				if (parsed != null) {
					parsedSystems.Add(parsed.Value);
				}
			}
			if (parsedSystems.Count == 0)
				return ParseValue<Schedulable>.Empty();
			return ParseValue<Schedulable>.Ok(
				new Schedulable {
					Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
					ClassName = classSymbol.Name,
					Systems = parsedSystems,
					Syntax = syntax
				});
		}

		public List<AutoSystemFunc> Systems;
		public string Namespace;
		public string ClassName;
		public TypeDeclarationSyntax Syntax;

		public string Generate(SourceProductionContext ctx)
		{
			HashSet<string> dedupedUsings = new HashSet<string>();
			dedupedUsings.Add("System");
			dedupedUsings.Add("Verse.ECS");
			dedupedUsings.Add("Verse.ECS.Systems");
			var parents = GetParents(Syntax);

			var sb = new StringBuilder();
			var usings = Syntax.GetFileUsings(ctx.CancellationToken);
			foreach (var use in usings) {
				dedupedUsings.Add(use.Name.ToString());
			}
			foreach (var use in dedupedUsings) {
				sb.AppendLine($"using {use};");
			}
			sb.AppendLine($"namespace {Namespace} {{");
			foreach (var parent in parents) {
				sb.AppendLine($"	public partial class {parent} {{");
			}
			sb.AppendLine($"    public partial class {ClassName} : Verse.Core.ISchedulable {{");

			AddSetsEnum(sb);
			AddSchedule(sb);

			for (var i = 0; i < parents.Count; i++) {
				sb.AppendLine($"	}}");
			}
			sb.AppendLine($"	}}");
			sb.AppendLine($"}}");
			sb.AppendLine($"}}");

			return sb.ToString();
		}

		private void AddSetsEnum(StringBuilder sb)
		{
			sb.AppendLine("public enum Sets {");
			foreach (var system in Systems) {
				sb.AppendLine($"    {system.Name},");
			}
			sb.AppendLine("All");
			sb.AppendLine("}");
		}

		private void AddSchedule(StringBuilder sb)
		{
			if (Syntax.HasMethod("Schedule", ["App"], "App")) {
				return;
			}
			sb.AppendLine("public App Schedule(App app) {");
			sb.AppendLine($"var t = typeof({ClassName});");
			foreach (var sys in Systems) {
				var generics = GenHelpers.GenerateSequence(sys.Params.Count, ", ", j => sys.Params[j].TypeToken);
				if (generics.Length > 0) {
					generics = $"<{generics}>";
				}
				sb.AppendLine($@"app = ScheduleAttribute.ScheduleFromMethod(
					app, 
					FuncSystem.Of{generics}({sys.Name}, ""{ClassName}.{sys.Name}"", new MethodSystemSet<{ClassName}>(""{sys.Name}""), SharedSystemComponent<{ClassName}>.RegisterWrite).
						InSet(new EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.{sys.Name})). 
						InSet(new EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.All)), 
					t.GetMethod(nameof({sys.Name}))!);");
			}
			sb.AppendLine("return app;");
		}

		private Stack<string> GetParents(TypeDeclarationSyntax syntax)
		{
			// If were a nested class, we need to replicate the parent structure
			Stack<string> parents = new Stack<string>();
			var rootNode = Syntax;
			while (rootNode.Parent is TypeDeclarationSyntax parent) {
				rootNode = parent;
				parents.Push(rootNode.Identifier.ValueText);
			}
			return parents;
		}
	}
}