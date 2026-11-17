namespace CsvHelper.Excel;

/// <summary>What a token of a compiled format code is.</summary>
internal enum FormatTokenKind : byte
{
	/// <summary>Text shown as it is, from the code's literal text (<see cref="FormatCode.Literal"/>): quoted text, escapes, characters that show as themselves, a space for <c>_x</c>.</summary>
	Literal,

	/// <summary>A digit placeholder, <c>0</c>, <c>#</c> or <c>?</c> (<see cref="FormatToken.Placeholder"/>), whose role is <see cref="FormatToken.Role"/>.</summary>
	Digit,

	/// <summary>A number's decimal point, which shows the culture's decimal separator.</summary>
	Point,

	/// <summary>An exponent, <c>E+</c> or <c>E-</c> (<see cref="FormatToken.Placeholder"/> is its sign).</summary>
	Exponent,

	/// <summary>The text, <c>@</c>.</summary>
	At,

	/// <summary>A number in General.</summary>
	General,

	/// <summary>A year: two digits for <c>y</c> and <c>yy</c>, four for more.</summary>
	Year,

	/// <summary>A month: its number for one or two letters, its abbreviated name for three, its first letter for five, its name otherwise.</summary>
	Month,

	/// <summary>A day: its number for one or two letters, its day of the week's abbreviated name for three, its name for more.</summary>
	Day,

	/// <summary>A day of the week's name: abbreviated for <c>aaa</c>, whole for more.</summary>
	WeekDay,

	/// <summary>An hour.</summary>
	Hour,

	/// <summary>A minute: <c>m</c> or <c>mm</c> after an hour or before a second.</summary>
	Minute,

	/// <summary>A second.</summary>
	Second,

	/// <summary>Fractions of a second: the <c>0</c>s after a <c>.</c> in a date or time (<see cref="FormatToken.Count"/> of them).</summary>
	SubSecond,

	/// <summary>Elapsed hours, <c>[h]</c>, padded to its letters.</summary>
	ElapsedHours,

	/// <summary>Elapsed minutes, <c>[m]</c>.</summary>
	ElapsedMinutes,

	/// <summary>Elapsed seconds, <c>[s]</c>.</summary>
	ElapsedSeconds,

	/// <summary><c>AM/PM</c> in any case: the designators of the language.</summary>
	AmPm,

	/// <summary><c>A/P</c>: its two letters as written (<see cref="FormatCode.Literal"/>, the first before noon).</summary>
	ShortAmPm,

	/// <summary>An era's year, <c>e</c>: the year, in four digits.</summary>
	EraYear,

	/// <summary>An era's name, <c>g</c>, which isn't rendered (Phase 8's decision 8).</summary>
	Era,

	/// <summary>A Buddhist year, <c>b</c>: two digits for one or two letters, four for more.</summary>
	BuddhistYear,
}

/// <summary>What a digit placeholder does in its section.</summary>
internal enum PlaceholderRole : byte
{
	/// <summary>Nothing: a placeholder in a date or text section, which shows <c>0</c> for <c>0</c> and nothing else.</summary>
	None,

	/// <summary>An integer placeholder, or a fraction's whole number's.</summary>
	Integer,

	/// <summary>A decimal placeholder, after the point.</summary>
	Decimal,

	/// <summary>An exponent's placeholder.</summary>
	Exponent,

	/// <summary>A fraction's numerator's placeholder.</summary>
	Numerator,

	/// <summary>A fraction's denominator's placeholder.</summary>
	Denominator,
}

/// <summary>A token of a compiled format code.</summary>
/// <param name="kind">What it is.</param>
/// <param name="start">Where its text starts in the code's literal text, for a literal or <c>A/P</c>.</param>
/// <param name="length">How many bytes its text has.</param>
/// <param name="count">A date or time token's letters, or the digits of fractions of a second.</param>
/// <param name="placeholder">A digit placeholder's character, or an exponent's sign.</param>
/// <param name="role">What a digit placeholder does.</param>
/// <param name="ofDay">Whether an elapsed time follows a date part, where it is the time of day in its unit.</param>
internal readonly struct FormatToken(FormatTokenKind kind, int start = 0, int length = 0, int count = 0, char placeholder = '\0', PlaceholderRole role = PlaceholderRole.None, bool ofDay = false)
{
	/// <summary>What it is.</summary>
	public FormatTokenKind Kind { get; } = kind;

	/// <summary>Where its text starts in the code's literal text, for a literal or <c>A/P</c>.</summary>
	public int Start { get; } = start;

	/// <summary>How many bytes its text has.</summary>
	public int Length { get; } = length;

	/// <summary>A date or time token's letters (<c>yyyy</c> is 4), or the digits of fractions of a second.</summary>
	public int Count { get; } = count;

	/// <summary>A digit placeholder's character (<c>0</c>, <c>#</c> or <c>?</c>), or an exponent's sign (<c>+</c> or <c>-</c>).</summary>
	public char Placeholder { get; } = placeholder;

	/// <summary>What a digit placeholder does.</summary>
	public PlaceholderRole Role { get; } = role;

	/// <summary>Whether an elapsed time follows a date part, where it is the time of day in its unit (<c>yyyy-mm-dd [h]:mm</c>).</summary>
	public bool OfDay { get; } = ofDay;
}
