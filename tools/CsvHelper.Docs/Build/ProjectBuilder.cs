using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CsvHelper.Docs.Build;

/// <summary>What the compiler got for one target framework of the library.</summary>
/// <param name="TargetFramework">The target framework moniker.</param>
/// <param name="AssemblyPath">The built DLL.</param>
/// <param name="XmlPath">The XML documentation file next to it.</param>
/// <param name="References">The reference assemblies the compiler used.</param>
/// <param name="SourceFiles">The C# files the compiler used, generated ones included.</param>
/// <param name="DefineConstants">The preprocessor symbols.</param>
/// <param name="LangVersion">The C# language version setting.</param>
internal sealed record BuildOutput(
	string TargetFramework,
	string AssemblyPath,
	string XmlPath,
	IReadOnlyList<string> References,
	IReadOnlyList<string> SourceFiles,
	IReadOnlyList<string> DefineConstants,
	string LangVersion);

/// <summary>
/// Builds the library for one target framework and asks MSBuild, in the same call, for the exact references, sources
/// and preprocessor symbols the compiler used (<c>-getItem</c> with <c>-t:Build</c> reports them after the build).
/// </summary>
internal static class ProjectBuilder
{
	public static BuildOutput Build(string projectPath, string targetFramework, bool restore, bool lenient, TimeSpan timeout)
	{
		var arguments = new List<string>
		{
			"build", projectPath, "-c", "Release", "-f", targetFramework, "-t:Build", "--disable-build-servers", "-nologo",
			"-getProperty:TargetPath", "-getProperty:DefineConstants", "-getProperty:LangVersion",
			"-getItem:ReferencePathWithRefAssemblies", "-getItem:Compile",
		};
		if (!restore) arguments.Add("--no-restore");
		// Old tags must not fail on warnings that a newer SDK adds.
		if (lenient) arguments.Add("-p:TreatWarningsAsErrors=false");
		var directory = Path.GetDirectoryName(projectPath)!;
		var result = ProcessRunner.Run("dotnet", arguments, directory, timeout);
		var json = result.Output.Trim();
		if (result.ExitCode != 0 || !json.StartsWith('{'))
		{
			var lines = result.Combined.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(10);
			throw new InvalidOperationException($"Building {Path.GetFileName(projectPath)} ({targetFramework}) failed:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
		}

		using var document = JsonDocument.Parse(json);
		var properties = document.RootElement.GetProperty("Properties");
		var items = document.RootElement.GetProperty("Items");
		var targetPath = properties.GetProperty("TargetPath").GetString()!;

		return new BuildOutput(
			targetFramework,
			targetPath,
			Path.ChangeExtension(targetPath, ".xml"),
			Paths(items, "ReferencePathWithRefAssemblies"),
			Paths(items, "Compile"),
			(properties.GetProperty("DefineConstants").GetString() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
			properties.GetProperty("LangVersion").GetString() ?? "latest");
	}

	/// <summary>The target frameworks a project declares (an old tag may lack some).</summary>
	public static IReadOnlyList<string> TargetFrameworks(string projectPath)
	{
		// Two properties, so that MSBuild answers in JSON.
		var result = ProcessRunner.Run("dotnet", ["msbuild", projectPath, "-getProperty:TargetFrameworks", "-getProperty:TargetFramework", "-nologo"],
			Path.GetDirectoryName(projectPath)!, TimeSpan.FromMinutes(2));
		if (result.ExitCode != 0) throw new InvalidOperationException($"Could not read the target frameworks of {projectPath}: {result.Combined.Trim()}");
		using var document = JsonDocument.Parse(result.Output);
		var properties = document.RootElement.GetProperty("Properties");
		var list = properties.GetProperty("TargetFrameworks").GetString();
		if (string.IsNullOrWhiteSpace(list)) list = properties.GetProperty("TargetFramework").GetString();

		return (list ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	/// <summary>Builds every target framework of a project, one after the other (they share obj/project.assets.json).</summary>
	public static IReadOnlyList<BuildOutput> BuildAll(string projectPath, IReadOnlyList<string> targetFrameworks, bool lenient, TimeSpan timeout)
	{
		var outputs = new List<BuildOutput>();
		for (var i = 0; i < targetFrameworks.Count; i++)
		{
			Log.Info($"  building {Path.GetFileName(projectPath)} for {targetFrameworks[i]}");
			outputs.Add(Build(projectPath, targetFrameworks[i], restore: i == 0, lenient, timeout));
		}

		return outputs;
	}

	private static IReadOnlyList<string> Paths(JsonElement items, string name)
	{
		if (!items.TryGetProperty(name, out var list)) return [];

		return list.EnumerateArray().Select(i => i.GetProperty("FullPath").GetString()!).ToList();
	}
}
