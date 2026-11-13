using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The names of a conversion's files: a CSV file for each worksheet of <see cref="ExcelConvert.ToCsvFiles"/>, named after
/// its sheet and made a file name on every system.
/// </summary>
/// <remarks>
/// <para>
/// A sheet's name becomes a file's (<see cref="FileStem"/>): <c>&lt; &gt; : " / \ | ? *</c>, control characters and lone
/// surrogates are <c>_</c>, so that no name is a path; it is cut to <see cref="MaxStemLength"/> UTF-16 code units, which
/// every sheet name Excel writes fits, so that a crafted one can't pass a file system's limit; dots and spaces at its end
/// are dropped, as Windows drops them; a name of Windows' devices (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>,
/// <c>COM0</c> to <c>COM9</c> and <c>LPT0</c> to <c>LPT9</c>, with superscript digits too), alone or before a dot, gets
/// <c>_</c> before it; and an empty one is <c>Sheet</c> and the sheet's number.
/// </para>
/// <para>
/// Names then taken to be the same are numbered as Excel numbers a copy of a sheet, <c>Data (2)</c>, by the key of
/// <see cref="SheetNames.Key"/>, which compares them ignoring case and Unicode's forms as file systems that ignore case or
/// normalize names (Windows', macOS') do, and more. The first in tab order keeps its name, and a number is one no other
/// sheet's name has.
/// </para>
/// </remarks>
internal static class ConversionNames
{
	/// <summary>The most UTF-16 code units of a file's name before its number and extension: twice a sheet name's 31.</summary>
	public const int MaxStemLength = 64;

	/// <summary>The extension of a CSV file.</summary>
	private const string CsvExtension = ".csv";

	/// <summary>The characters a file name can't have on Windows, besides control characters; <c>/</c> is the only one elsewhere.</summary>
	private static readonly char[] Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

	/// <summary>Windows' device names, which a file name can't be, with or without an extension.</summary>
	private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
	{
		"CON", "PRN", "AUX", "NUL",
		"COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
		"LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
	};

	/// <summary>The names of the CSV files of sheets, in their order: each sheet's name made a file name, numbered when names are taken to be the same.</summary>
	/// <param name="sheets">Each sheet's name and its number, from 1, in tab order.</param>
	/// <returns>The file names, with <c>.csv</c>.</returns>
	public static string[] FileNames(IReadOnlyList<(string Name, int Number)> sheets)
	{
		var stems = new string[sheets.Count];
		var keys = new string[sheets.Count];
		// Every sheet's own name is kept from the numbers, so that a sheet named "Data (2)" keeps its name.
		var own = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < stems.Length; i++)
		{
			stems[i] = FileStem(sheets[i].Name, sheets[i].Number);
			keys[i] = SheetNames.Key(stems[i]);
			own.Add(keys[i]);
		}

		var used = new HashSet<string>(StringComparer.Ordinal);
		var names = new string[stems.Length];
		for (var i = 0; i < stems.Length; i++)
		{
			var name = stems[i];
			if (!used.Add(keys[i]))
			{
				for (var number = 2L; ; number++)
				{
					var copy = $"{stems[i]} ({number.ToString(CultureInfo.InvariantCulture)})";
					var key = SheetNames.Key(copy);
					if (!own.Contains(key) && used.Add(key))
					{
						name = copy;

						break;
					}
				}
			}

			names[i] = name + CsvExtension;
		}

		return names;
	}

	/// <summary>A sheet's name made a file's name, without its extension or a number (see <see cref="ConversionNames"/>).</summary>
	/// <param name="name">The sheet's name, as the workbook has it: any string.</param>
	/// <param name="number">The sheet's number, from 1, for an empty name.</param>
	/// <returns>The file's name, which is never empty.</returns>
	public static string FileStem(string name, int number)
	{
		var text = new StringBuilder(Math.Min(name.Length, MaxStemLength));
		for (var i = 0; i < name.Length && text.Length < MaxStemLength; i++)
		{
			var c = name[i];
			if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
			{
				// A pair, kept whole, or not at all when only half fits.
				if (text.Length + 2 > MaxStemLength)
				{
					break;
				}

				text.Append(c).Append(name[++i]);
			}
			else
			{
				text.Append(char.IsControl(c) || char.IsSurrogate(c) || Array.IndexOf(Forbidden, c) >= 0 ? '_' : c);
			}
		}

		var end = text.Length;
		while (end > 0 && text[end - 1] is '.' or ' ')
		{
			end--;
		}

		if (end == 0)
		{
			return "Sheet" + number.ToString(CultureInfo.InvariantCulture);
		}

		var stem = text.ToString(0, end);
		var dot = stem.IndexOf('.');

		return Devices.Contains((dot < 0 ? stem : stem.Substring(0, dot)).TrimEnd(' ')) ? "_" + stem : stem;
	}
}
