using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Verse.ECS.Generator;

[Generator]
public class SystemContainerGenerator : IIncrementalGenerator
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

		return c.BaseList != null && c.BaseList.Types.Select(x => x.Type.ToString().Equals("SystemContainer")).Any();

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


	public class AutoSystemContainer
	{
		public static ParseValue<AutoSystemContainer> TryParseClass(GeneratorSyntaxContext ctx, CancellationToken cancel)
		{
			var syntax = ctx.Node as TypeDeclarationSyntax;
			if (syntax is null)
				return ParseValue<AutoSystemContainer>.Empty();

			if (ctx.SemanticModel.GetDeclaredSymbol(syntax) is not { } classSymbol)
				return ParseValue<AutoSystemContainer>.Empty();

			if (!syntax.IsPartial())
				return ParseValue<AutoSystemContainer>.Err(Diagnostic.Create(Diagnostics.MissingPartial, syntax.GetLocation()));

			// TODO what about non scheduled containers?
			var systems = syntax.MethodsWithAttribute("Schedule", cancel);
			if (systems.Count == 0)
				return ParseValue<AutoSystemContainer>.Err(Diagnostic.Create(Diagnostics.MissingSystems, syntax.GetLocation()));

			var parsedSystems = new List<AutoSystemFunc>();
			foreach (var system in systems) {
				var parsed = AutoSystemFunc.TryParse(ctx, system, cancel);
				if (parsed != null) {
					parsedSystems.Add(parsed.Value);
				}
			}
			if (parsedSystems.Count == 0)
				return ParseValue<AutoSystemContainer>.Empty();
			return ParseValue<AutoSystemContainer>.Ok(
				new AutoSystemContainer {
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

		private bool HasScheduleSystems()
		{
			return Systems.Any(sys => sys.IsScheduled);
		}

		public string Generate(SourceProductionContext ctx)
		{
			HashSet<string> dedupedUsings = new HashSet<string>();
			dedupedUsings.Add("System");
			dedupedUsings.Add("Verse.ECS");
			dedupedUsings.Add("Verse.ECS.Systems");
			var parents = GetParents(Syntax);
			var interfaces = "";
			if (HasScheduleSystems()) {
				interfaces = ": Verse.ECS.Systems.ISchedulable";
			}

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
			sb.AppendLine($"    public partial class {ClassName} {interfaces} {{");

			AddSetsEnum(sb);
			AddSystemRegistration(sb);
			AddIntoConfigs(sb);
			AddIntoSet(sb);
			if (HasScheduleSystems()) {
				AddSchedule(sb);
			}

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

		private void AddSystemRegistration(StringBuilder sb)
		{
			sb.AppendLine($@"public class SystemEntries {{
	public SystemEntries({ClassName} container) {{");

			foreach (var sys in Systems) {
				var generics = GenHelpers.GenerateSequence(sys.Params.Count, ", ", j => sys.Params[j].TypeToken);
				if (generics.Length > 0) {
					generics = $"<{generics}>";
				}
				sb.AppendLine($"{sys.Name} = new FuncSystem{generics}(container.{sys.Name}, \"{ClassName}.{sys.Name}\", new EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.{sys.Name}), SharedSystemComponent<{ClassName}>.RegisterWrite);");
			}
			sb.AppendLine("}");

			foreach (var sys in Systems) {
				var generics = GenHelpers.GenerateSequence(sys.Params.Count, ", ", j => sys.Params[j].TypeToken);
				if (generics.Length > 0) {
					generics = $"<{generics}>";
				}
				sb.AppendLine($"    public readonly FuncSystem{generics} {sys.Name};");
			}
			sb.AppendLine("}");

			sb.AppendLine(@"private SystemEntries _systemEntries;

public SystemEntries Systems {
	get {
		_systemEntries ??= new SystemEntries(this);
		return _systemEntries;
	}
}
");
		}

		private void AddIntoSet(StringBuilder sb)
		{
			if (Syntax.HasMethod("IntoSystemSet", [], "ISystemSet")) {
				return;
			}
			sb.AppendLine($@"public override Verse.ECS.Systems.ISystemSet IntoSystemSet() {{
	return new Verse.ECS.Systems.EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.All);
}}
");
		}

		private void AddIntoConfigs(StringBuilder sb)
		{
			if (Syntax.HasMethod("IntoConfigs", [], "NodeConfigs<ISystemSet>")) {
				return;
			}
			var reg = GenHelpers.GenerateSequence(Systems.Count, ",\n", i => $"SystemConfigAttribute.ApplyAllFromMethod(Systems.{Systems[i].Name}, t.GetMethod(nameof({Systems[i].Name}))!)");
			sb.AppendLine($@"public override NodeConfigs<ISystem> IntoConfigs() {{
			var t = GetType();
			return NodeConfigs<ISystem>.Of(
				{reg}
			).InSet(new EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.All));
		}}");
		}

		private void AddSchedule(StringBuilder sb)
		{
			if (Syntax.HasMethod("Schedule", ["App"], "App")) {
				return;
			}
			sb.AppendLine("public App Schedule(App app) {");
			sb.AppendLine($"var t = typeof({ClassName});");
			foreach (var sys in Systems) {
				sb.AppendLine($"app = ScheduleAttribute.ScheduleFromMethod(app, Systems.{sys.Name}.InSet(new EnumSystemSet<{ClassName}.Sets>({ClassName}.Sets.All)), t.GetMethod(nameof({sys.Name}))!);");
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