using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Verse.ECS.Generator;

public struct AutoSystemFunc
{
	public List<AutoParam> Params;
	public string Name;
	public bool IsScheduled;
	public static AutoSystemFunc? TryParse(GeneratorSyntaxContext ctx, MethodDeclarationSyntax method, CancellationToken cancel = default)
	{
		var autoParams = new List<AutoParam>();
		foreach (var param in method.ParameterList.Parameters) {
			var autoParam = AutoParam.TryCreate(ctx, method, param);
			// We need every param
			if (!autoParam.HasValue) {
				return null;
			}
			autoParams.Add(autoParam.Value);
		}
		var isScheduled = false;
		foreach (var attrList in method.AttributeLists) {
			foreach (var attr in attrList.Attributes) {
				if (attr.Name.ToString() == "Schedule" || attr.Name.ToString() == "ScheduleAttribute") {
					isScheduled = true;
				}
			}
		}
		return new AutoSystemFunc { Params = autoParams, Name = method.Identifier.ValueText, IsScheduled = isScheduled };
	}
}