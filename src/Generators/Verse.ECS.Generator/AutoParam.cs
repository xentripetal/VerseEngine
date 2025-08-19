using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Verse.ECS.Generator;

public struct AutoParam
{
	public static AutoParam? TryCreate(GeneratorSyntaxContext ctx, MethodDeclarationSyntax method, ParameterSyntax param)
	{
		if (param.Type is null) {
			return null;
		}
		var model = ctx.SemanticModel.GetTypeInfo(param.Type);
		if (model.Type is null)
			return null;

		var typeInfo = model.Type;
		var access = AccessModifier.None;
		foreach (var mod in param.Modifiers) {
			if (mod.Text == "in") {
				access = AccessModifier.In;
			} else if (mod.Text == "ref") {
				access = AccessModifier.Ref;
			} else {
				return null;
			}
		}
		var kind = ParamType.Unknown;
		var iinto = typeInfo.AllInterfaces.First(x => x.Name.Equals("IIntoSystemParam"));
		if (iinto is not null) {
			kind = ParamType.IInto;
		} else {
			return null;
		}
		return new AutoParam {
			Type = kind,
			Access = access,
			Method = method,
			Name = param.Identifier.ValueText,
			SemanticType = model,
			TypeToken = param.Type.ToFullString(),
		};
	}

	public enum ParamType
	{
		Unknown,
		IInto,
		Res
	}

	public enum AccessModifier
	{
		Ref,
		In,
		None
	}

	public TypeInfo SemanticType;
	public string TypeToken;
	public ParamType Type;
	public AccessModifier Access;
	public string Name;


	public MethodDeclarationSyntax Method;

}