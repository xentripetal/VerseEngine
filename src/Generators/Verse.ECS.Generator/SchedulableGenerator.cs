using System;
using System.Diagnostics;
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
		// wait for debugger
		//Debugger.Launch();

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
			dedupedUsings.Add("System.Reflection");
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

			AddSchedule(sb);
			AddSetsEnum(sb);
			AddSystemClasses(sb);

			for (var i = 0; i < parents.Count; i++) {
				sb.AppendLine($"	}}");
			}
			sb.AppendLine($"	}}");
			sb.AppendLine($"}}");

			return sb.ToString();
		}

		private void AddSystemClasses(StringBuilder sb)
		{
			foreach (var system in Systems) {
				var name = $"{system.Name}System";
				var constructorParameters = system.IsStatic ? "" :  ClassName + " systems";
				var constructorBody = system.IsStatic ? "" : "_systems = systems;";
				var extraProps = system.IsStatic ? "" : $"private {ClassName} _systems;";
				var extraInit = system.IsStatic ? "" : $"Meta.Access.AddUnfilteredWrite(world.GetComponent<Verse.ECS.Systems.SharedSystemComponent<{ClassName}>>().Id);";
				var methodCall = system.IsStatic ? $"{ClassName}.{system.Name}" : $"_systems.{system.Name}";
				var attributes = GenHelpers.GenerateSequence(system.Attributes.Count, "\n", i => $"[{system.Attributes[i]}]");

				sb.AppendLine($@"
{attributes}
public partial class {name} : Verse.ECS.Systems.ClassSystem {{
	public {name}({constructorParameters}) {{
		{constructorBody}
	}}

	{GenHelpers.GenerateSequence(system.Params.Count, "\n", j => $"private {system.Params[j].GenParamType()} _p{j};")}
	{extraProps}

	public override System.Collections.Generic.List<Verse.ECS.Systems.ISystemSet> GetDefaultSystemSets() {{
		return [Set, new Verse.ECS.Systems.EnumSet<{ClassName}.Sets>({ClassName}.Sets.{system.Name}), new Verse.ECS.Systems.EnumSet<{ClassName}.Sets>({ClassName}.Sets.All)];
	}}

	public override void Initialize(World world) {{	
		{GenHelpers.GenerateSequence(system.Params.Count, "\n", j => $"_p{j} = {system.Params[j].GenInitializer()};")}
		SetParams({GenHelpers.GenerateSequence(system.Params.Count, ", ", j => $"_p{j}")});
		{extraInit}
		base.Initialize(world);
	}}

	public override void Run(World world) {{
		{methodCall}({GenHelpers.GenerateSequence(system.Params.Count, ", ", j => system.Params[j].GenCaller($"_p{j}"))});	
	}}
}}");
			}
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
			foreach (var sys in Systems) {
				var systemReference = sys.IsStatic ? $"new {ClassName}.{sys.Name}System()" : $"new {ClassName}.{sys.Name}System(this)";
				sb.AppendLine(string.IsNullOrEmpty(sys.ScheduleLabel)
					? $"app = app.AddSystems({systemReference});"
					: $"app = app.AddSystems({sys.ScheduleLabel}, {systemReference});");
			}
			sb.AppendLine("return app;");
			sb.AppendLine("}");
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