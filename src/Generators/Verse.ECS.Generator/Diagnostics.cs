using Microsoft.CodeAnalysis;

namespace Verse.ECS.Generator;

public static class Diagnostics
{
	public static readonly DiagnosticDescriptor MissingPartial = new DiagnosticDescriptor(
		id: "VECS.G001",
		title: "Class or struct must be made partial",
		messageFormat: "Class or struct must be made partial",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor MissingSystems = new DiagnosticDescriptor(
		id: "VECSG002",
		title: "Class or struct must contain atleast one AutoSystem method",
		messageFormat: "Class or struct must contain a AutoSystem method",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor MultipleRunMethods = new DiagnosticDescriptor(
		id: "VECS.G003",
		title: "Class or struct must contain only one Run Method",
		messageFormat: "Class or struct must contain only one Run method",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor AutoParamInternalError = new DiagnosticDescriptor(
		id: "VECS.G004",
		title: "An unknown problem occurred while parsing an AutoParam",
		messageFormat: "An unknown problem occurred while parsing an AutoParam, please report this issue",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor AutoParamMultipleProviders = new DiagnosticDescriptor(
		id: "VECS.G005",
		title: "AutoParam has multiple param providers",
		messageFormat: "AutoParam has multiple param providers, only one is allowed",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor AutoParamOutParam = new DiagnosticDescriptor(
		id: "VECS.G005",
		title: "Run method has out parameter",
		messageFormat: "Run method has out parameter, this is not supported",
		category: "Configuration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);
}