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
		if (IsFromWorldParam(typeInfo)) {
			kind = ParamType.IFromWorld;
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

	private static bool IsFromWorldParam(ITypeSymbol type)
	{
		// has IFromWorld and ISystemParam
		bool hasISystemParam = false;
		bool hasFromWorld = false;
		var interfaces = type.AllInterfaces.ToList();
		
		// If its a generic, check constraints. This isn't the perfect solution as it won't allow nested constraints.
		// TODO see if theres a better way to do this
		if (type is ITypeParameterSymbol symbol) {
			interfaces = symbol.ConstraintTypes.Select(x => (INamedTypeSymbol)x).ToList();
		}
		foreach (var iface in interfaces) {
			if (iface.Name.Equals("ISystemParam")) {
				hasISystemParam = true;
			}
			if (iface.Name.Equals("IFromWorld")) {
				hasFromWorld = true;
			}
		}
		return hasISystemParam && hasFromWorld;
	}

	public enum ParamType
	{
		Unknown,
		IFromWorld,
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
		AccessModifier.In          => "in ",
		AccessModifier.Ref         => "ref ",
		AccessModifier.RefReadonly => "in ",
		AccessModifier.Readonly    => "", // can't actually happen
		AccessModifier.None        => "",
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
			ParamType.Res    => $"Verse.ECS.Res<{TypeToken}>",
			ParamType.ResMut => $"Verse.ECS.ResMut<{TypeToken}>",
			_                => TypeToken
		};
	}

	public string GenInitializer()
	{
		return Type switch {
			ParamType.IFromWorld => $"{TypeToken}.FromWorld(world)",
			ParamType.Res        => $"Res<{TypeToken}>.FromWorld(world)",
			ParamType.ResMut     => $"ResMut<{TypeToken}>.FromWorld(world)",
			_                    => "default"
		};
	}

	public string GenCaller(string paramName)
	{
		var modifier = AccessModifierText;
		if (Type == ParamType.IFromWorld)
			return modifier + paramName;
		if (Type == ParamType.ResMut && Access == AccessModifier.Ref)
			return $"{modifier}{paramName}.MutValue";
		if (Type == ParamType.ResMut || Type == ParamType.Res)
			return $"{modifier}{paramName}.Value";
		return paramName;
	}
}