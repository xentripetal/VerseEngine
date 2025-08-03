#region License
/* MoonWorks - Game Development Framework
 * Copyright 2021 Evan Hemsley
 */

/* Derived from code by Ethan Lee (Copyright 2009-2021).
 * Released under the Microsoft Public License.
 * See fna.LICENSE for details.
 */
#endregion

#region Using Statements
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml;
#endregion

namespace Verse.MoonWorks;

internal static class MoonWorksDllMap
{

        #region Private Static Methods
	private static string GetPlatformName()
	{
		if (OperatingSystem.IsWindows()) {
			return "windows";
		}
		if (OperatingSystem.IsMacOS()) {
			return "osx";
		}
		if (OperatingSystem.IsLinux()) {
			return "linux";
		}
		if (OperatingSystem.IsFreeBSD()) {
			return "freebsd";
		}
		// Maybe this platform statically links?
		return "unknown";
	}
        #endregion
        #region Private Static Variables
	private static readonly Dictionary<string, string> mapDictionary = new Dictionary<string, string>();
	private static readonly Dictionary<string, string[]> mapDependencies = new Dictionary<string, string[]>();
	private static readonly HashSet<string> loadedLibraries = new HashSet<string>();
        #endregion

        #region DllImportResolver Callback Method
	private static IntPtr MapAndLoad(
		string libraryName,
		Assembly assembly,
		DllImportSearchPath? dllImportSearchPath
	)
	{
		var mappedName = mapDictionary.GetValueOrDefault(libraryName, libraryName);
		LoadDeps(libraryName, assembly, dllImportSearchPath);
		loadedLibraries.Add(mappedName);
		return NativeLibrary.Load(mappedName, assembly, dllImportSearchPath);
	}

	private static void LoadDeps(string library, Assembly assembly, DllImportSearchPath? dllImportSearchPath)
	{
		if (loadedLibraries.Contains(library)) {
			return;
		}

		foreach (var dependency in mapDependencies.GetValueOrDefault(library, [])) {
			var depMappedName = mapDictionary.GetValueOrDefault(dependency, dependency);

			loadedLibraries.Add(dependency);
			LoadDeps(dependency, assembly, dllImportSearchPath);
			var depHandle = NativeLibrary.Load(depMappedName, assembly, dllImportSearchPath);
			if (depHandle == IntPtr.Zero) {
				throw new DllNotFoundException($"Failed to load dependency: {dependency}");
			}
		}
	}
        #endregion

        #region Module Initializer
	private static void LoadConfigFile(
		string filePath,
		string os,
		string cpu,
		string wordsize,
		Assembly assembly
	)
	{
		// Log it in the debugger for non-console apps.
		if (Debugger.IsAttached) {
			Debug.WriteLine($"Loading DLL map {filePath}");
		}
		var xmlDoc = new XmlDocument();
		xmlDoc.Load(filePath);

		// The NativeLibrary API cannot remap function names. :(
		if (xmlDoc.GetElementsByTagName("dllentry").Count > 0) {
			var msg = "Function remapping is not supported by .NET Core. Ignoring dllentry elements...";
			Console.WriteLine(msg);

			// Log it in the debugger for non-console apps.
			if (Debugger.IsAttached) {
				Debug.WriteLine(msg);
			}
		}

		foreach (XmlNode node in xmlDoc.GetElementsByTagName("dllmap")) {
			XmlAttribute? attribute;

			// Check the OS
			attribute = node.Attributes["os"];
			if (attribute != null && !FilterMatches(attribute.Value, os)) {
				continue;
			}

			// Check the CPU
			attribute = node.Attributes["cpu"];
			if (attribute != null && !FilterMatches(attribute.Value, cpu)) {
				continue;
			}

			// Check the word size
			attribute = node.Attributes["wordsize"];
			if (attribute != null && !FilterMatches(attribute.Value, wordsize)) {
				continue;
			}

			// Get the actual library names
			var oldLib = node.Attributes["dll"]?.Value;
			var newLib = node.Attributes["target"]?.Value;
			var rawDeps = node.Attributes["dependsOn"]?.Value;

			if (string.IsNullOrWhiteSpace(oldLib) || string.IsNullOrWhiteSpace(newLib)) {
				continue;
			}

			var deps = rawDeps?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(d => d.Trim())
				.Where(d => !string.IsNullOrWhiteSpace(d))
				.ToArray() ?? [];

			if (mapDictionary.TryAdd(oldLib, newLib)) {
				mapDependencies[newLib] = deps;
			}
		}
	}

	private static bool FilterMatches(string filter, string phrase)
	{
		if (string.IsNullOrWhiteSpace(filter) || string.IsNullOrWhiteSpace(phrase)) {
			return false;
		}

		if (filter.StartsWith("!")) {
			return !phrase.Contains(filter.Substring(1), StringComparison.OrdinalIgnoreCase);
		}

		return phrase.Contains(filter, StringComparison.OrdinalIgnoreCase);
	}


	[ModuleInitializer]
	public static void Init()
	{
		// Ignore NativeAOT platforms since they don't perform dynamic loading.
		if (!RuntimeFeature.IsDynamicCodeSupported) {
			return;
		}

		// Get the platform and architecture
		var os = GetPlatformName();
		var cpu = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
		var wordsize = (IntPtr.Size * 8).ToString();

		// Get the path to the assembly
		var assembly = Assembly.GetExecutingAssembly();
		var assemblyPath = AppContext.BaseDirectory;

		// look for any *.dll.config files in the same directory as the assembly
		var files = Directory.GetFiles(assemblyPath);
		foreach (var file in files) {
			if (file.EndsWith(".dll.config", StringComparison.OrdinalIgnoreCase)) {
				// Load the config file
				LoadConfigFile(file, os, cpu, wordsize, assembly);
			}
		}

		// Set the resolver callback for our native assemblies
		NativeLibrary.SetDllImportResolver(assembly, MapAndLoad);
	}
        #endregion
}