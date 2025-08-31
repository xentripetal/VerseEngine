using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Verse.ECS.Generator;

public struct AutoSystemFunc
{
	public List<AutoParam> Params;
	public string Name;
	public bool IsScheduled;
	public bool IsStatic;
	public bool IsPublic;
	public string? ScheduleLabel;
	public List<string> Attributes;

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
		var scheduleLabel = "";
		List<string> attributes = new ();
		foreach (var attrList in method.AttributeLists) {
			foreach (var attr in attrList.Attributes) {
				if (attr.Name.ToString() == "Schedule" || attr.Name.ToString() == "ScheduleAttribute") {
					isScheduled = true;
					if (attr.ArgumentList != null) {
						foreach (var args in attr.ArgumentList.Arguments) {
							scheduleLabel = args.Expression.ToString();
						}
					}
				} else {
					attributes.Add(attr.ToString());
				}
			}
		}

		var isStatic = method.Modifiers.Any(modifier => modifier.ToString() == "static");
		return new AutoSystemFunc {
			Params = autoParams,
			Name = method.Identifier.ValueText,
			IsScheduled = isScheduled,
			IsStatic = isStatic,
			Attributes = attributes,
			IsPublic = method.Modifiers.Any(modifier => modifier.ToString() == "public"),
			ScheduleLabel = scheduleLabel
		};
	}
}