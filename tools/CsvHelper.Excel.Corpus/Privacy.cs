using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Checks that the corpus's files identify no one: producers record the author (Excel records the Office account's
/// email address), the folder a file was saved in, and the like, and the files are committed to a public repository.
/// </summary>
internal static partial class Privacy
{
	/// <summary>
	/// Fails when a file in a folder holds the user's name, the machine's name, the user's profile folder, or an email
	/// address, in any part of a zip, or anywhere in another file: as UTF-8, Latin-1, or UTF-16 at either byte alignment.
	/// </summary>
	/// <param name="folder">The folder of files to check.</param>
	/// <exception cref="InvalidOperationException">A file holds one of them.</exception>
	public static void Check(string folder)
	{
		var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var needles = new[] { Environment.UserName, Environment.MachineName, profile, profile.Replace('\\', '/') }
			.Where(n => n.Length >= 3)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var problems = new List<string>();
		foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
		{
			foreach (var (part, bytes) in Contents(file))
			{
				foreach (var text in Decodings(bytes))
				{
					var found = needles.FirstOrDefault(n => text.Contains(n, StringComparison.OrdinalIgnoreCase)) ?? Email().Match(text) switch
					{
						{ Success: true } match => "an email address (" + match.Value.Length + " characters)",
						_ => null,
					};
					if (found is not null)
					{
						problems.Add($"{file}{(part.Length > 0 ? "!" + part : "")}: {(needles.Contains(found) ? "the user's or machine's name or folder" : found)}");

						break;
					}
				}
			}
		}

		if (problems.Count > 0)
		{
			throw new InvalidOperationException("Files hold personal information; don't commit them:\n  " + string.Join("\n  ", problems.Distinct()));
		}
	}

	/// <summary>The parts of a zip, or the whole of another file.</summary>
	/// <param name="file">The file.</param>
	/// <returns>Each part's name (empty for a whole file) and bytes.</returns>
	private static IEnumerable<(string Part, byte[] Bytes)> Contents(string file)
	{
		var bytes = File.ReadAllBytes(file);
		if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K')
		{
			yield return ("", bytes);

			yield break;
		}

		using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
		foreach (var entry in zip.Entries)
		{
			using var stream = entry.Open();
			var part = new MemoryStream();
			stream.CopyTo(part);

			yield return (entry.FullName, part.ToArray());
		}
	}

	/// <summary>The ways a file's strings can be stored.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The bytes decoded each way.</returns>
	private static IEnumerable<string> Decodings(byte[] bytes)
	{
		yield return Encoding.UTF8.GetString(bytes);
		yield return Encoding.Latin1.GetString(bytes);
		yield return Encoding.Unicode.GetString(bytes);

		if (bytes.Length > 1)
		{
			yield return Encoding.Unicode.GetString(bytes, 1, bytes.Length - 1);
		}
	}

	/// <summary>An email address.</summary>
	/// <returns>The expression.</returns>
	[GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}")]
	private static partial Regex Email();
}
