using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// Files fetched rather than written: the corpus's files from other projects' test data, which <c>downloads.txt</c>
/// lists, and the checked download that POI's jars use too.
/// </summary>
internal static class Downloads
{
	/// <summary>
	/// Fetches each file <c>downloads.txt</c> lists into the corpus folder, when it is missing there or doesn't match its
	/// SHA-256.
	/// </summary>
	/// <param name="files">The corpus folder.</param>
	/// <param name="list">The <c>downloads.txt</c> file: a <c>path url sha256</c> line per file.</param>
	public static void Write(string files, string list)
	{
		using var client = new HttpClient();
		foreach (var line in File.ReadLines(list).Select(l => l.Split('#')[0].Trim()).Where(l => l.Length > 0))
		{
			var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var (target, url, expected) = (parts[0], parts[1], parts[2]);
			var path = Path.Combine(files, target.Replace('/', Path.DirectorySeparatorChar));
			if (!File.Exists(path) || Sha256(File.ReadAllBytes(path)) != expected)
			{
				var bytes = Fetch(client, url, expected);
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllBytes(path, bytes);
			}

			Console.WriteLine($"  {path}");
		}
	}

	/// <summary>Downloads a file and checks it against its pinned SHA-256.</summary>
	/// <param name="client">The client.</param>
	/// <param name="url">The file's URL.</param>
	/// <param name="sha256">Its SHA-256, in lowercase hexadecimal.</param>
	/// <returns>The file's bytes.</returns>
	/// <exception cref="InvalidOperationException">The file doesn't match the hash.</exception>
	public static byte[] Fetch(HttpClient client, string url, string sha256)
	{
		var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
		var actual = Sha256(bytes);
		if (actual != sha256)
		{
			throw new InvalidOperationException($"{url} has the SHA-256 {actual}, not the {sha256} it is pinned to.");
		}

		return bytes;
	}

	/// <summary>The SHA-256 of some bytes, in lowercase hexadecimal.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash.</returns>
	public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
