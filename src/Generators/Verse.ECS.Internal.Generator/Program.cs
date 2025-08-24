using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
#pragma warning disable IDE0055
#pragma warning disable IDE0008
#pragma warning disable IDE0058

namespace TinyEcs.Generator;

[Generator]
public sealed class MyGenerator : IIncrementalGenerator
{
	private const int MAX_GENERICS = 16;

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(postContext => {
			//postContext.AddSource("Verse.ECS.Systems.Scheduler.g.cs", CodeFormatter.Format(GenerateSchedulerSystems()));
			//postContext.AddSource("Verse.ECS.Systems.StateHandlers.g.cs", CodeFormatter.Format(GenerateSchedulerSystemsState()));
			//postContext.AddSource("Verse.ECS.Systems.StageSpecific.g.cs", CodeFormatter.Format(GenerateSchedulerStageSpecificSystems()));
			//postContext.AddSource("Verse.ECS.Systems.Interfaces.g.cs", CodeFormatter.Format(GenerateSystemsInterfaces()));
			postContext.AddSource("Verse.ECS.Systems.DataAndFilter.g.cs", CodeFormatter.Format(CreateDataAndFilterStructs()));
			postContext.AddSource("Verse.ECS.Systems.FuncSystem.g.cs", CodeFormatter.Format(GenerateFuncSystems()));

			postContext.AddSource("Verse.ECS.Archetypes.g.cs", CodeFormatter.Format(GenerateArchetypes()));
			postContext.AddSource("Verse.ECS.QueryIteratorEach.g.cs", CodeFormatter.Format(GenerateQueryIteratorEach()));
		});

		static string GenerateArchetypes()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Collections.Generic;

                namespace Verse.ECS
                {{
					{GenerateArchetypeSigns()}
                }}

                #pragma warning restore 1591
            ";
		}

		static string GenerateQueryIteratorEach()
		{
			return $@"
			#pragma warning disable 1591
                #nullable enable

                using System;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{GenerateIterators()}
					#endif
                }}

                #pragma warning restore 1591
			";
		}

		static string GenerateIterators()
		{
			var sb = new StringBuilder();

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var generics = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var whereGenerics = GenerateSequence(i + 1, " ", j => $"where T{j} : struct");
				var ptrList = GenerateSequence(i + 1, "\n", j => $"private DataRow<T{j}> _current{j};");
				var ptrSet = GenerateSequence(i + 1, "\n", j => $"_current{j} = _iterator.GetColumn<T{j}>({j});");
				var ptrAdvance = GenerateSequence(i + 1, "\n", j => $"_current{j}.Next();");
				var fieldSign = GenerateSequence(i + 1, ", ", j => $"out Ptr<T{j}> ptr{j}");
				var fieldAssignments = GenerateSequence(i + 1, "\n", j => $"ptr{j} = _current{j}.Value;");
				var queryBuilderCalls = GenerateSequence(i + 1, "\n", j => $"builder.With<T{j}>();");
				var mutators = GenerateSequence(i+1, "\n", j => $"if (_current{j}.Value.Mutated) _iterator.MarkChanged({j}, _index);");

				sb.AppendLine($@"
					[SkipLocalsInit]
					public unsafe ref struct Data<{generics}> : IData<Data<{generics}>>
						{whereGenerics}
					{{
						private QueryIterator _iterator;
						private int _index, _count;
						private ReadOnlySpan<ROEntityView> _entities;
						{ptrList}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						internal Data(QueryIterator queryIterator)
						{{
							_iterator = queryIterator;
							_index = -1;
							_count = -1;
						}}

						public static void Build(QueryBuilder builder)
						{{
							{queryBuilderCalls}
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						public static Data<{generics}> CreateIterator(QueryIterator iterator)
							=> new Data<{generics}>(iterator);

						[System.Diagnostics.CodeAnalysis.UnscopedRef]
						public ref Data<{generics}> Current
						{{
							[MethodImpl(MethodImplOptions.AggressiveInlining)]
							get => ref this;
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						public readonly void Deconstruct({fieldSign})
						{{
							{fieldAssignments}
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						public readonly void Deconstruct(out PtrRO<ROEntityView> entity, {fieldSign})
						{{
							entity = new (in _entities[_index]);
							{fieldAssignments}
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						public bool MoveNext()
						{{
							{mutators}
							if (++_index >= _count)
							{{
								if (!_iterator.Next())
									return false;

								{ptrSet}
								_entities = _iterator.Entities();

								_index = 0;
								_count = _iterator.Count;
							}}
							else
							{{
								{ptrAdvance}
							}}

							return true;
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						public readonly Data<{generics}> GetEnumerator() => this;
					}}
				");
			}

			return sb.ToString();
		}

		static string GenerateArchetypeSigns()
		{
			var sb = new StringBuilder();

			sb.AppendLine("public partial class World {");

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var generics = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var whereGenerics = GenerateSequence(i + 1, " ", j => $"where T{j} : struct");
				var objsArgs = GenerateSequence(i + 1, ", ", j => $"GetComponent<T{j}>()");

				sb.AppendLine($@"
					/// <inheritdoc cref=""World.Archetype(Span{{EcsID}})""/>
					public Archetype Archetype<{generics}>()
						{whereGenerics}
						=> Archetype({objsArgs});
				");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		static string CreateDataAndFilterStructs()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{CreateDataAndFilterStructsContent()}
					#endif
                }}

                #pragma warning restore 1591
            ";
		}

		static string CreateDataAndFilterStructsContent()
		{
			var sb = new StringBuilder();

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : struct, IFilter<T{j}>, allows ref struct");
				var appendTermsCalls = GenerateSequence(i + 1, "\n", j => $"T{j}.Build(builder);");
				var appendApplyCalls = GenerateSequence(i + 1, " | ", j => $"T{j}.Apply(in filter, row)\n");

				var subIterators = GenerateSequence(i + 1, "\n", j => $"private T{j} _iter{j};");
				var createSubIterators = GenerateSequence(i + 1, "\n", j => $"_iter{j} = T{j}.CreateIterator(iterator);");

				var callSubIterators = GenerateSequence(i + 1, "\n", j => $"var i{j} = _iter{j}.MoveNext();");
				var callResultsSubIterators = GenerateSequence(i + 1, " && ", j => $"i{j}");
				var setTicksSubIterators = GenerateSequence(i + 1, "\n", j => $"_iter{j}.SetTicks(lastRun, thisRun);");

				sb.AppendLine($@"
					public ref struct Filter<{genericsArgs}> : IFilter<Filter<{genericsArgs}>>
						{genericsArgsWhere}
					{{
						private QueryIterator _iterator;
						{subIterators}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						internal Filter(QueryIterator iterator)
						{{
							_iterator = iterator;
							{createSubIterators}
						}}

						public static void Build(QueryBuilder builder)
						{{
							{appendTermsCalls}
						}}

						[System.Diagnostics.CodeAnalysis.UnscopedRef]
						ref Filter<{genericsArgs}> IQueryIterator<Filter<{genericsArgs}>>.Current => ref this;

						static Filter<{genericsArgs}> IFilter<Filter<{genericsArgs}>>.CreateIterator(QueryIterator iterator)
						{{
							return new(iterator);
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						readonly Filter<{genericsArgs}> IQueryIterator<Filter<{genericsArgs}>>.GetEnumerator()
						{{
							return this;
						}}

						[MethodImpl(MethodImplOptions.AggressiveInlining)]
						bool IQueryIterator<Filter<{genericsArgs}>>.MoveNext()
						{{
							{callSubIterators}
							return {callResultsSubIterators};
						}}

						public void SetTicks(uint lastRun, uint thisRun)
						{{
							{setTicksSubIterators}
						}}
					}}
				");
			}

			return sb.ToString();
		}

		static string GenerateSchedulerSystems()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Collections.Generic;
                using System.Threading;
                using System.Threading.Tasks;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{GenerateSchedulerSystemsContent()}
					#endif
                }}

                #pragma warning restore 1591
            ";
		}

		static string GenerateSchedulerSystemsContent()
		{
			var sb = new StringBuilder();

			sb.AppendLine("public partial class Scheduler {");

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam<World>, IIntoSystemParam<World>");
				var objs = GenerateSequence(i + 1, "\n", j => $"T{j}? obj{j} = null;");
				var objsGen = GenerateSequence(i + 1, "\n", j => $"obj{j} ??= (T{j})T{j}.Generate(args);");
				var objsLock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Lock(ticks);");
				var objsUnlock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Unlock();");
				var systemCall = GenerateSequence(i + 1, ", ", j => $"obj{j}");
				var objsCheckInuse = GenerateSequence(i + 1, " ", j => $"obj{j}?.UseIndex != 0" + (j < i ? "||" : ""));

				sb.AppendLine($@"
				public FuncSystem<World> AddSystem<{genericsArgs}>(Action<{genericsArgs}> system, Stages stage = Stages.Update, ThreadingMode? threadingType = null)
					{genericsArgsWhere}
				{{
					if (!threadingType.HasValue)
						threadingType = ThreadingExecutionMode;

					{objs}
					var checkInuse = () => {objsCheckInuse};
					var fn = (SystemTicks ticks, World args, Func<SystemTicks, World, bool> runIf) =>
					{{
						if (runIf != null && !runIf.Invoke(ticks, args))
							return false;

						{objsGen}
						{objsLock}
						args.BeginDeferred();
						system({systemCall});
						args.EndDeferred();
						{objsUnlock}
						return true;
					}};
					var sys = new FuncSystem<World>(World, fn, checkInuse, stage, threadingType.Value);
					Add(sys, stage);
					return sys;
				}}
				");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		static string GenerateSchedulerSystemsState()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Collections.Generic;
                using System.Threading;
                using System.Threading.Tasks;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{GenerateSchedulerSystemsStateContent()}
					#endif
                }}

                #pragma warning restore 1591
            ";
		}

		static string GenerateSchedulerSystemsStateContent()
		{
			var sb = new StringBuilder();

			sb.AppendLine("public partial class Scheduler {");

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam<World>, IIntoSystemParam<World>");
				var objs = GenerateSequence(i + 1, "\n", j => $"T{j}? obj{j} = null;");
				var objsGen = GenerateSequence(i + 1, "\n", j => $"obj{j} ??= (T{j})T{j}.Generate(args);");
				var objsLock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Lock(ticks);");
				var objsUnlock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Unlock();");
				var systemCall = GenerateSequence(i + 1, ", ", j => $"obj{j}");
				var objsCheckInuse = GenerateSequence(i + 1, " ", j => $"obj{j}?.UseIndex != 0" + (j < i ? "||" : ""));

				// OnEnter method
				sb.AppendLine($@"
				public FuncSystem<World> OnEnter<TState, {genericsArgs}>(TState st, Action<{genericsArgs}> system, ThreadingMode? threadingType = null)
					where TState : struct, Enum
					{genericsArgsWhere}
				{{
					if (!threadingType.HasValue)
						threadingType = ThreadingExecutionMode;

					{objs}
					var stateChangeId = -1;
					var checkInuse = () => {objsCheckInuse};
					var fn = (SystemTicks ticks, World args, Func<SystemTicks, World, bool> runIf) =>
					{{
						if (runIf != null && !runIf.Invoke(ticks, args))
							return false;

						{objsGen}
						{objsLock}
						args.BeginDeferred();
						system({systemCall});
						args.EndDeferred();
						{objsUnlock}
						return true;
					}};
					var sys = new FuncSystem<World>(World, fn, checkInuse, Stages.OnEnter, threadingType.Value)
						.RunIf((State<TState> state) => state.ShouldEnter(st, ref stateChangeId));
					Add(sys, Stages.OnEnter);
					return sys;
				}}");

				// OnExit method
				sb.AppendLine($@"
				public FuncSystem<World> OnExit<TState, {genericsArgs}>(TState st, Action<{genericsArgs}> system, ThreadingMode? threadingType = null)
					where TState : struct, Enum
					{genericsArgsWhere}
				{{
					if (!threadingType.HasValue)
						threadingType = ThreadingExecutionMode;

					{objs}
					var stateChangeId = -1;
					var checkInuse = () => {objsCheckInuse};
					var fn = (SystemTicks ticks, World args, Func<SystemTicks, World, bool> runIf) =>
					{{
						if (runIf != null && !runIf.Invoke(ticks, args))
							return false;

						{objsGen}
						{objsLock}
						args.BeginDeferred();
						system({systemCall});
						args.EndDeferred();
						{objsUnlock}
						return true;
					}};
					var sys = new FuncSystem<World>(World, fn, checkInuse, Stages.OnExit, threadingType.Value)
						.RunIf((State<TState> state) => state.ShouldExit(st, ref stateChangeId));
					Add(sys, Stages.OnExit);
					return sys;
				}}");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		static string GenerateSchedulerStageSpecificSystems()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Collections.Generic;
                using System.Threading;
                using System.Threading.Tasks;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{GenerateSchedulerStageSpecificSystemsContent()}
					#endif
                }}

                #pragma warning restore 1591
            ";
		}

		static string GenerateSchedulerStageSpecificSystemsContent()
		{
			var sb = new StringBuilder();

			sb.AppendLine("public partial class Scheduler {");

			// Define the stage names to generate (excluding OnEnter and OnExit)
			var stageNames = new[] {
				("Startup", "OnStartup"),
				("FrameStart", "OnFrameStart"),
				("BeforeUpdate", "OnBeforeUpdate"),
				("Update", "OnUpdate"),
				("AfterUpdate", "OnAfterUpdate"),
				("FrameEnd", "OnFrameEnd")
			};

			foreach (var (stageName, methodName) in stageNames) {
				for (var i = 0; i < MAX_GENERICS; ++i) {
					var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
					var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam<World>, IIntoSystemParam<World>");
					var objs = GenerateSequence(i + 1, "\n", j => $"T{j}? obj{j} = null;");
					var objsGen = GenerateSequence(i + 1, "\n", j => $"obj{j} ??= (T{j})T{j}.Generate(args);");
					var objsLock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Lock(ticks);");
					var objsUnlock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Unlock();");
					var systemCall = GenerateSequence(i + 1, ", ", j => $"obj{j}");
					var objsCheckInuse = GenerateSequence(i + 1, " ", j => $"obj{j}?.UseIndex != 0" + (j < i ? "||" : ""));

					sb.AppendLine($@"
					public FuncSystem<World> {methodName}<{genericsArgs}>(Action<{genericsArgs}> system, ThreadingMode? threadingType = null)
						{genericsArgsWhere}
					{{
						if (!threadingType.HasValue)
							threadingType = ThreadingExecutionMode;

						{objs}
						var checkInuse = () => {objsCheckInuse};
						var fn = (SystemTicks ticks, World args, Func<SystemTicks, World, bool> runIf) =>
						{{
							if (runIf != null && !runIf.Invoke(ticks, args))
								return false;

							{objsGen}
							{objsLock}
							args.BeginDeferred();
							system({systemCall});
							args.EndDeferred();
							{objsUnlock}
							return true;
						}};
						var sys = new FuncSystem<World>(World, fn, checkInuse, Stages.{stageName}, threadingType.Value);
						Add(sys, Stages.{stageName});
						return sys;
					}}");
				}
			}

			// Also add versions that take just Action with no parameters
			foreach (var (stageName, methodName) in stageNames) {
				sb.AppendLine($@"
				public FuncSystem<World> {methodName}(Action system, ThreadingMode? threadingType = null)
				{{
					if (!threadingType.HasValue)
						threadingType = ThreadingExecutionMode;

					var sys = new FuncSystem<World>(World, (ticks, args, runIf) =>
					{{
						if (runIf?.Invoke(ticks, args) ?? true)
						{{
							system();
							return true;
						}}
						return false;
					}}, () => false, Stages.{stageName}, threadingType.Value);
					Add(sys, Stages.{stageName});
					return sys;
				}}");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		static string GenerateSystemsInterfaces()
		{
			return $@"
                #pragma warning disable 1591
                #nullable enable

                using System;
                using System.Collections.Generic;
                using System.Threading;
                using System.Runtime.CompilerServices;

                namespace Verse.ECS
                {{
					#if NET9_0_OR_GREATER
					{GenerateSystemsInterfacesContent()}
					#endif
                }}

                #pragma warning restore 1591
            ";
		}

		static string GenerateSystemsInterfacesContent()
		{
			var sb = new StringBuilder();
			sb.AppendLine("public sealed partial class FuncSystem<TArg> {");

			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam, IIntoSystemParam");
				var objs = GenerateSequence(i + 1, "\n", j => $"T{j}? obj{j} = null;");
				var objsGen = GenerateSequence(i + 1, "\n", j => $"obj{j} ??= (T{j})T{j}.Generate(args);");
				var objsLock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Lock(ticks);");
				var objsUnlock = GenerateSequence(i + 1, "\n", j => $"obj{j}.Unlock();");
				var systemCall = GenerateSequence(i + 1, ", ", j => $"obj{j}");
				var objsCheckInuse = GenerateSequence(i + 1, " ", j => $"obj{j}?.UseIndex != 0" + (j < i ? "||" : ""));

				sb.AppendLine($@"
					public FuncSystem<TArg> RunIf<{genericsArgs}>(Func<{genericsArgs}, bool> condition)
						{genericsArgsWhere}
					{{
						{objs}
						var fn = (SystemTicks ticks, TArg args) => {{
							{objsGen}
							return condition({systemCall});
						}};
						_conditions.Add(fn);
						return this;
					}}
				");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		static string GenerateFuncSystems()
		{
			var sb = new StringBuilder();

			sb.AppendLine("using Verse.ECS;");
			sb.AppendLine("using Verse.ECS.Systems;");


			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam, IIntoSystemParam<T{j}>");
				var paramArgs = GenerateSequence(i + 1, "\n\t", j => $"private T{j} _p{j} = default;");
				var paramArgsGen = GenerateSequence(i + 1, "\n\t\t", j => $"_p{j} = T{j}.Generate(world);");
				var paramArgsList = GenerateSequence(i + 1, ", ", j => $"_p{j}");

				sb.AppendLine($@"
public sealed partial class FuncSystem<{genericsArgs}>(Action<{genericsArgs}> fn, string? name = null, ISystemSet set = null, Action<World, ISystem>? OnInit = null) : BaseFuncSystem(name, set, OnInit)
	{genericsArgsWhere} {{

	{paramArgs}

	public override void Initialize(World world) {{
		{paramArgsGen}
		SetParams({paramArgsList});
		base.Initialize(world);
	}}

	public override void Run(World world) {{
		fn({paramArgsList});
	}}
}}
");
			}

			sb.AppendLine("public partial class FuncSystem {");
			for (var i = 0; i < MAX_GENERICS; ++i) {
				var genericsArgs = GenerateSequence(i + 1, ", ", j => $"T{j}");
				var genericsArgsWhere = GenerateSequence(i + 1, "\n", j => $"where T{j} : class, ISystemParam, IIntoSystemParam<T{j}>");
				sb.AppendLine($@"public static FuncSystem<{genericsArgs}> Of<{genericsArgs}>(Action<{genericsArgs}> fn, string? name = null, ISystemSet set = null) {genericsArgsWhere} => new FuncSystem<{genericsArgs}>(fn, name, set);");
			}
			sb.AppendLine("}");

			return sb.ToString();
		}

		static string GenerateSequence(int count, string separator, Func<int, string> generator)
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
	}
}

internal sealed class CodeFormatter : CSharpSyntaxRewriter
{
	public static string Format(string source)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);
		var normalized = syntaxTree.GetRoot().NormalizeWhitespace();

		normalized = new CodeFormatter().Visit(normalized);

		return normalized.ToFullString();
	}

	private static T FormatMembers<T>(T node, IEnumerable<SyntaxNode> members) where T : SyntaxNode
	{
		SyntaxNode[] membersArray = members as SyntaxNode[] ?? members.ToArray();

		var memberCount = membersArray.Length;
		var current = 0;

		return node.ReplaceNodes(membersArray, RewriteTrivia);

		SyntaxNode RewriteTrivia<TNode>(TNode oldMember, TNode _) where TNode : SyntaxNode
		{
			var trailingTrivia = oldMember.GetTrailingTrivia().ToFullString().TrimEnd() + "\n\n";
			return current++ != memberCount - 1
				? oldMember.WithTrailingTrivia(SyntaxFactory.Whitespace(trailingTrivia))
				: oldMember;
		}
	}

	public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) => base.VisitNamespaceDeclaration(FormatMembers(node, node.Members))!;

	public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) => base.VisitClassDeclaration(FormatMembers(node, node.Members))!;

	public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node) => base.VisitStructDeclaration(FormatMembers(node, node.Members))!;
}