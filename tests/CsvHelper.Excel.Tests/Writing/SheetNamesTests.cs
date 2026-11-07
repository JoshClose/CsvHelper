namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The rules for sheet names, as Excel opened and refused them through COM on 2026-11-06, and the key that names Excel
/// takes to be the same share.
/// </summary>
public class SheetNamesTests
{
	/// <summary>Names Excel opens, which the writer accepts.</summary>
	public static IEnumerable<object[]> Accepted() =>
		new[]
		{
			"History", "TRUE", "A1", "R1C1", "123", " ", " a", "a ", "=a", "+a", "a!b#c$d%e&f;g,h\"i<j>k", "x'y", "Q1 Orders",
			new string('x', 31), new string('x', 29) + Emoji, "日本語", "a_x0041_b", "a" + (char)0xA0 + "b",
		}.Select(n => new object[] { n });

	/// <summary>Names the writer refuses, and the start of each message.</summary>
	public static IEnumerable<object[]> Refused()
	{
		yield return ["", "Sheet names are 1 to 31 characters long, and \"\" has 0."];
		yield return [new string('x', 32), "Sheet names are 1 to 31 characters long, and \"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\" has 32."];
		yield return [new string('x', 30) + Emoji, "Sheet names are 1 to 31 characters long"];
		foreach (var c in "\\/?*[]:")
		{
			yield return ["a" + c + "b", $"A sheet name can't have \\, /, ?, *, [, ] or :, and \"a{c}b\" has {c}."];
		}

		yield return ["'x", "A sheet name can't start or end with an apostrophe: \"'x\"."];
		yield return ["x'", "A sheet name can't start or end with an apostrophe: \"x'\"."];
		yield return ["a\tb", "A sheet name can't have control characters, and \"a\\tb\" has U+0009."];
		yield return ["a\nb", "A sheet name can't have control characters, and \"a\\nb\" has U+000A."];
		yield return ["a" + (char)1 + "b", "A sheet name can't have control characters, and \"a\\u0001b\" has U+0001."];
		yield return ["a" + (char)0x7F, "A sheet name can't have control characters"];
		yield return ["a" + (char)0x85, "A sheet name can't have control characters"];
		yield return ["a" + (char)0x9F, "A sheet name can't have control characters"];
	}

	/// <summary>Pairs of names Excel refuses together, which the writer takes to be the same.</summary>
	public static IEnumerable<object[]> Same()
	{
		yield return ["Data", "data"];
		yield return ["Äpfel", "äpfel"];
		yield return ["straße", "STRASSE"];
		yield return ["Straße", "Stra" + (char)0x1E9E + "e"];
		yield return [((char)0x3C3).ToString(), ((char)0x3C2).ToString()];
		yield return ["K", ((char)0x212A).ToString()];
		yield return [((char)0xE9).ToString(), "e" + (char)0x301];
		yield return [((char)0xFB01).ToString(), "fi"];
	}

	/// <summary>Pairs of names Excel opens together, which the writer takes to be different.</summary>
	public static IEnumerable<object[]> Different()
	{
		yield return ["e", ((char)0xE9).ToString()];
		yield return [((char)0x3042).ToString(), ((char)0x30A2).ToString()];
		yield return ["I", ((char)0x131).ToString()];
		yield return ["i", ((char)0x130).ToString()];
		yield return ["ab", "a" + (char)0x200B + "b"];
		yield return ["Data", "Data2"];
	}

	/// <summary>An emoji, two UTF-16 code units.</summary>
	private static string Emoji => char.ConvertFromUtf32(0x1F600);

	[Theory]
	[MemberData(nameof(Accepted))]
	public void AcceptsWhatExcelOpens(string name)
	{
		SheetNames.Check(name, "name");
	}

	[Theory]
	[MemberData(nameof(Refused))]
	public void RefusesWhatExcelDoesNot(string name, string message)
	{
		var e = Assert.Throws<ArgumentException>(() => SheetNames.Check(name, "name"));

		Assert.StartsWith(message, e.Message);
		Assert.Equal("name", e.ParamName);
	}

	[Fact]
	public void RefusesLoneSurrogates()
	{
		// Not theory data, which xUnit passes through UTF-8, making them U+FFFD.
		var high = Assert.Throws<ArgumentException>(() => SheetNames.Check("a" + (char)0xD83D, "name"));
		var low = Assert.Throws<ArgumentException>(() => SheetNames.Check((char)0xDE00 + "a", "name"));

		Assert.StartsWith("A sheet name can't have a lone surrogate, and \"a\\uD83D\" has U+D83D.", high.Message);
		Assert.StartsWith("A sheet name can't have a lone surrogate, and \"\\uDE00a\" has U+DE00.", low.Message);
		Assert.Throws<ArgumentException>(() => SheetNames.Check("a" + (char)0xDE00 + (char)0xD83D, "name"));
		SheetNames.Check("a" + (char)0xD83D + (char)0xDE00, "name");
	}

	[Theory]
	[MemberData(nameof(Same))]
	public void TakesNamesExcelTakesToBeTheSameToBeTheSame(string first, string second)
	{
		Assert.Equal(SheetNames.Key(first), SheetNames.Key(second));
	}

	[Theory]
	[MemberData(nameof(Different))]
	public void TakesNamesExcelTellsApartToBeDifferent(string first, string second)
	{
		Assert.NotEqual(SheetNames.Key(first), SheetNames.Key(second));
	}

	[Fact]
	public void TakesCompatibilityFormsToBeTheSame()
	{
		// Stricter than Excel, which opens these together: full-width and half-width forms.
		Assert.Equal(SheetNames.Key("A"), SheetNames.Key(((char)0xFF21).ToString()));
		Assert.Equal(SheetNames.Key(((char)0xFF76).ToString()), SheetNames.Key(((char)0x30AB).ToString()));
	}
}
