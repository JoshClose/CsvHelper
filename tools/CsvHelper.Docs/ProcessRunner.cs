using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Docs;

internal sealed record ProcessResult(int ExitCode, string Output, string Error)
{
	public string Combined => Output + Error;
}

/// <summary>Runs git and dotnet.</summary>
internal static class ProcessRunner
{
	public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, TimeSpan? timeout = null)
	{
		using var process = Start(fileName, arguments, workingDirectory);
		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();
		Wait(process, fileName, timeout);

		return new ProcessResult(process.ExitCode, output.Result, error.Result);
	}

	/// <summary>Runs a command whose output is binary (git archive).</summary>
	public static byte[] RunBinary(string fileName, IEnumerable<string> arguments, string workingDirectory, TimeSpan? timeout = null)
	{
		using var process = Start(fileName, arguments, workingDirectory);
		using var buffer = new MemoryStream();
		var copy = process.StandardOutput.BaseStream.CopyToAsync(buffer);
		var error = process.StandardError.ReadToEndAsync();
		Wait(process, fileName, timeout);
		copy.Wait();
		if (process.ExitCode != 0) throw new InvalidOperationException($"{fileName} failed: {error.Result}");

		return buffer.ToArray();
	}

	/// <summary>Runs a command and returns its standard output, throwing when it fails.</summary>
	public static string Output(string fileName, IEnumerable<string> arguments, string workingDirectory)
	{
		var result = Run(fileName, arguments, workingDirectory);
		if (result.ExitCode != 0)
			throw new InvalidOperationException($"{fileName} {string.Join(' ', arguments)} failed ({result.ExitCode}): {result.Error.Trim()}");

		return result.Output;
	}

	private static Process Start(string fileName, IEnumerable<string> arguments, string workingDirectory)
	{
		var info = new ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
		};
		foreach (var argument in arguments) info.ArgumentList.Add(argument);
		// Keep MSBuild and the compiler from leaving server processes behind (they lock files in temp folders).
		info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
		info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
		info.Environment["DOTNET_NOLOGO"] = "1";

		return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {fileName}.");
	}

	private static void Wait(Process process, string fileName, TimeSpan? timeout)
	{
		if (process.WaitForExit(timeout ?? TimeSpan.FromMinutes(30))) return;
		try
		{
			process.Kill(entireProcessTree: true);
		}
		catch (InvalidOperationException)
		{
			// It exited after all.
		}

		throw new TimeoutException($"{fileName} did not finish within {timeout}.");
	}
}
