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
				access = access == AccessModifier.Readonly ? AccessModifier.RefReadonly : AccessModifier.Ref;
			} else if (mod.Text == "readonly") {
				access = access == AccessModifier.Ref ? AccessModifier.RefReadonly : AccessModifier.Readonly;
			}
		}
		var kind = ParamType.Unknown;
		var iinto = typeInfo.AllInterfaces.FirstOrDefault(x => x.Name.Equals("IIntoSystemParam"));
		if (iinto is not null) {
			kind = ParamType.IInto;
		} else {
			kind = ParamType.ResMut;
			if (access is AccessModifier.In or AccessModifier.RefReadonly) {
				kind = ParamType.Res;
			}
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
		Res,
		ResMut,
	}

	public enum AccessModifier
	{
		None,
		Readonly,
		RefReadonly,
		Ref,
		In,
	}

	public string AccessModifierText => Access switch {
		AccessModifier.In   => "in ",
		AccessModifier.Ref  => "ref ",
		AccessModifier.RefReadonly => "in ",
		AccessModifier.Readonly => "", // can't actually happen
		AccessModifier.None => "",
	};

	public TypeInfo SemanticType;
	public string TypeToken;
	public ParamType Type;
	public AccessModifier Access;
	public string Name;


	public MethodDeclarationSyntax Method;

	public string GenParamType()
	{
		return Type switch {
			ParamType.Res    => $"Res<{TypeToken}>",
			ParamType.ResMut => $"ResMut<{TypeToken}>",
			_                => TypeToken
		};
	}

	public string GenInitializer()
	{
		return Type switch {
			ParamType.IInto  => $"{TypeToken}.Generate(world)",
			ParamType.Res    => $"world.GetRes<{TypeToken}>()",
			ParamType.ResMut => $"world.GetResMut<{TypeToken}>()",
			_                => "default"
		};
	}

	public string GenCaller(string paramName)
	{
		var modifier = AccessModifierText;
		if (Type == ParamType.IInto)
			return modifier + paramName;
		if (Type == ParamType.ResMut || Type == ParamType.Res)
			return $"{modifier}{paramName}.Value";
		return paramName;
	}
}