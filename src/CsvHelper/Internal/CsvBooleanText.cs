using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper;

/// <summary>
/// The true and false values of <see cref="CsvReaderOptions.TrueValues"/> and <see cref="CsvReaderOptions.FalseValues"/>,
/// matched as the built-in ones are: ignoring ASCII case and surrounding spaces and tabs. An unset list keeps its
/// built-in values.
/// </summary>
internal sealed class CsvBooleanText
{
	private readonly byte[][]? trueValues;
	private readonly byte[][]? falseValues;

	/// <summary>Prepares the values for matching.</summary>
	/// <param name="trueValues">The values that read as <see langword="true"/>, or <see langword="null"/> for the built-in ones.</param>
	/// <param name="falseValues">The values that read as <see langword="false"/>, or <see langword="null"/> for the built-in ones.</param>
	public CsvBooleanText(IReadOnlyList<string>? trueValues, IReadOnlyList<string>? falseValues)
	{
		this.trueValues = Encode(trueValues);
		this.falseValues = Encode(falseValues);
	}

	/// <summary>Parses <paramref name="s"/> as one of the values.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="hide">Whether the message of the exception leaves the value out.</param>
	/// <returns>The value it reads as.</returns>
	/// <exception cref="FormatException"><paramref name="s"/> is none of the values.</exception>
	public bool Parse(ReadOnlySpan<byte> s, bool hide) => TryParse(s, out var value) ? value : throw CsvParse.NotBoolean(s, hide);

	/// <summary>Tries to parse <paramref name="s"/> as one of the values.</summary>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="value">The value it reads as, or <see langword="false"/> when it is none of them.</param>
	/// <returns>Whether <paramref name="s"/> is one of the values.</returns>
	public bool TryParse(ReadOnlySpan<byte> s, out bool value)
	{
		var trimmed = s.Trim(" \t"u8);
		if (trueValues is null ? CsvParse.TryBoolean(trimmed, out value) && value : Matches(trimmed, trueValues))
		{
			value = true;

			return true;
		}

		if (falseValues is null ? CsvParse.TryBoolean(trimmed, out value) && !value : Matches(trimmed, falseValues))
		{
			value = false;

			return true;
		}

		value = false;

		return false;
	}

	private static bool Matches(ReadOnlySpan<byte> s, byte[][] values)
	{
		foreach (var value in values)
		{
			if (Ascii.EqualsIgnoreCase(s, value))
			{
				return true;
			}
		}

		return false;
	}

	private static byte[][]? Encode(IReadOnlyList<string>? values)
	{
		if (values is null)
		{
			return null;
		}

		var encoded = new byte[values.Count][];
		for (var i = 0; i < encoded.Length; i++)
		{
			encoded[i] = Encoding.UTF8.GetBytes(values[i].Trim(' ', '\t'));
		}

		return encoded;
	}
}
