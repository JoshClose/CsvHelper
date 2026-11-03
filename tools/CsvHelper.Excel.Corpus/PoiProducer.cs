using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// The corpus's Apache POI files: <c>java/Corpus.java</c>, run by a JDK outside the repository with the jars that
/// <c>java/jars.txt</c> pins, each downloaded once from Maven Central and checked against its SHA-256.
/// </summary>
internal static class PoiProducer
{
	/// <summary>Maven Central, where POI and its dependencies are published.</summary>
	private const string MavenCentral = "https://repo1.maven.org/maven2/";

	/// <summary>
	/// Writes <c>poi/xssf.xlsx</c> and <c>poi/sxssf.xlsx</c> with the JDK given, or the newest <c>jdk-*</c> folder in
	/// the tool's folder outside the repository.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="scripts">The folder of <c>Corpus.java</c> and <c>jars.txt</c>.</param>
	/// <param name="java">A <c>java.exe</c> or a JDK's folder, from <c>--java</c>; <see langword="null"/> for the default.</param>
	public static void Write(string files, string scripts, string? java)
	{
		var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CsvHelper.Excel.Corpus");
		var program = FindJava(home, java);
		var jars = Jars(Path.Combine(scripts, "jars.txt"), Path.Combine(home, "jars"));
		var spec = Path.Combine(Path.GetTempPath(), "CsvHelper.Excel.Corpus." + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(spec, Basic.ToJson());

			// The source launcher compiles Corpus.java in memory, so nothing is written beside it.
			Program.Run(program, "-cp", string.Join(Path.PathSeparator, jars), Path.Combine(scripts, "Corpus.java"), spec, files);
		}
		finally
		{
			File.Delete(spec);
		}
	}

	/// <summary>Finds the <c>java.exe</c> to run: the one given, or the newest JDK unpacked into the tool's folder.</summary>
	/// <param name="home">The tool's folder outside the repository.</param>
	/// <param name="java">A <c>java.exe</c> or a JDK's folder; <see langword="null"/> for the default.</param>
	/// <returns>The path of <c>java.exe</c>.</returns>
	/// <exception cref="InvalidOperationException">There is no JDK there.</exception>
	private static string FindJava(string home, string? java)
	{
		if (java is not null)
		{
			var path = Directory.Exists(java) ? Path.Combine(java, "bin", "java.exe") : java;

			return File.Exists(path) ? path : throw new InvalidOperationException($"--java names no java.exe: {path}.");
		}

		var found = Directory.Exists(home)
			? Directory.EnumerateDirectories(home, "jdk-*")
				.Select(folder => Path.Combine(folder, "bin", "java.exe"))
				.Where(File.Exists)
				.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault()
			: null;

		return found ?? throw new InvalidOperationException(
			$"There is no JDK in {home}. Unpack the zip of a JDK 17 or later there (Eclipse Temurin's, for one), or pass --java with its java.exe or folder.");
	}

	/// <summary>
	/// The jars <c>jars.txt</c> pins, in the folder given: each one there already is checked, and each one missing, or
	/// that doesn't match, is downloaded from Maven Central and checked.
	/// </summary>
	/// <param name="list">The <c>jars.txt</c> file: a <c>group:artifact:version sha256</c> line per jar.</param>
	/// <param name="folder">The folder that holds the jars.</param>
	/// <returns>The jars' paths, in the list's order.</returns>
	/// <exception cref="InvalidOperationException">A download doesn't match its pinned SHA-256.</exception>
	private static List<string> Jars(string list, string folder)
	{
		Directory.CreateDirectory(folder);
		using var client = new HttpClient();
		var jars = new List<string>();
		foreach (var line in File.ReadLines(list).Select(l => l.Split('#')[0].Trim()).Where(l => l.Length > 0))
		{
			var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var coordinate = parts[0].Split(':');
			var (group, artifact, version) = (coordinate[0], coordinate[1], coordinate[2]);
			var expected = parts[1];
			var path = Path.Combine(folder, $"{artifact}-{version}.jar");
			if (!File.Exists(path) || Sha256(File.ReadAllBytes(path)) != expected)
			{
				var url = $"{MavenCentral}{group.Replace('.', '/')}/{artifact}/{version}/{artifact}-{version}.jar";
				var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
				var actual = Sha256(bytes);
				if (actual != expected)
				{
					throw new InvalidOperationException($"{url} has the SHA-256 {actual}, not the {expected} that jars.txt pins.");
				}

				File.WriteAllBytes(path, bytes);
				Console.WriteLine($"  downloaded {artifact}-{version}.jar");
			}

			jars.Add(path);
		}

		return jars;
	}

	/// <summary>The SHA-256 of some bytes, in lowercase hexadecimal.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash.</returns>
	private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
