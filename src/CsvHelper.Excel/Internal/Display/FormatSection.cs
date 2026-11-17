namespace CsvHelper.Excel;

/// <summary>The operator of a section's condition (<c>[&gt;=100]</c>).</summary>
internal enum ConditionOperator : byte
{
	/// <summary>No condition.</summary>
	None,

	/// <summary><c>=</c>.</summary>
	Equal,

	/// <summary><c>&lt;&gt;</c>.</summary>
	NotEqual,

	/// <summary><c>&lt;</c>.</summary>
	Less,

	/// <summary><c>&lt;=</c>.</summary>
	LessOrEqual,

	/// <summary><c>&gt;</c>.</summary>
	Greater,

	/// <summary><c>&gt;=</c>.</summary>
	GreaterOrEqual,
}

/// <summary>The system format a locale code names, which a section shows whatever else it holds.</summary>
internal enum SystemFormat : byte
{
	/// <summary>None.</summary>
	None,

	/// <summary>The culture's long date: <c>[$-F800]</c>, <c>[$-x-sysdate]</c>.</summary>
	LongDate,

	/// <summary>The culture's long time: <c>[$-F400]</c>, <c>[$-x-systime]</c>.</summary>
	LongTime,
}

/// <summary>What a section of a compiled format code shows.</summary>
internal enum FormatSectionKind : byte
{
	/// <summary>A number: placeholders, General, or literals alone.</summary>
	Number,

	/// <summary>A fraction: placeholders, a <c>/</c>, and a denominator of placeholders or a number.</summary>
	Fraction,

	/// <summary>A date, a time or elapsed time.</summary>
	Date,

	/// <summary>The text section: text and booleans, at each <c>@</c>.</summary>
	Text,
}

/// <summary>A section of a compiled format code (<see cref="FormatCode"/>): its tokens, and what they make of it.</summary>
internal sealed class FormatSection
{
	/// <summary>The tokens, in order, with adjacent literals merged; commas that group or scale, and fills, are left out.</summary>
	public FormatToken[] Tokens { get; internal set; } = [];

	/// <summary>What the section shows.</summary>
	public FormatSectionKind Kind { get; internal set; }

	/// <summary>The operator of its condition, or <see cref="ConditionOperator.None"/>.</summary>
	public ConditionOperator Condition { get; internal set; }

	/// <summary>The number its condition compares with.</summary>
	public double Operand { get; internal set; }

	/// <summary>Whether it has a color (<c>[Red]</c>, <c>[Color10]</c>), which doesn't change its text.</summary>
	public bool HasColor { get; internal set; }

	/// <summary>Whether it has <c>[DBNum1]</c> to <c>[DBNum3]</c>, which isn't rendered (Phase 8's decision 8).</summary>
	public bool HasDBNum { get; internal set; }

	/// <summary>Whether it has a calendar, <c>B1</c> or <c>B2</c>, which isn't rendered.</summary>
	public bool HasCalendar { get; internal set; }

	/// <summary>Whether it has a locale code (<c>[$-407]</c>, <c>[$€-407]</c>, <c>[$USD]</c>).</summary>
	public bool HasLocale { get; internal set; }

	/// <summary>The names a locale code's language gives, or <see langword="null"/> for the culture's.</summary>
	public DateNames? Names { get; internal set; }

	/// <summary>
	/// The system format the section shows whatever else it holds: the one its locale code names in the first section, and
	/// in a later date section the first section's, as Excel reads them.
	/// </summary>
	public SystemFormat System { get; internal set; }

	/// <summary>Whether it has <c>General</c>.</summary>
	public bool HasGeneral { get; internal set; }

	/// <summary>How many digit placeholders a number or fraction has.</summary>
	public int Placeholders { get; internal set; }

	/// <summary>How many integer placeholders a number has, or a fraction's whole number.</summary>
	public int IntegerPlaceholders { get; internal set; }

	/// <summary>The integer placeholders' characters, in order: <c>#,##0</c> has <c>###0</c>.</summary>
	public string IntegerKinds { get; internal set; } = "";

	/// <summary>How many decimal placeholders a number has.</summary>
	public int DecimalPlaceholders { get; internal set; }

	/// <summary>Whether a number groups its integer digits by three: a <c>,</c> between integer placeholders.</summary>
	public bool Grouping { get; internal set; }

	/// <summary>How many times a number is divided by 1,000: a <c>,</c> right after its last placeholder, or right before its point, each.</summary>
	public int Scale { get; internal set; }

	/// <summary>How many <c>%</c> it has, each of which multiplies a number by 100, unless it has an exponent.</summary>
	public int Percents { get; internal set; }

	/// <summary>Whether a number has an exponent.</summary>
	public bool HasExponent { get; internal set; }

	/// <summary>How many placeholders an exponent has.</summary>
	public int ExponentPlaceholders { get; internal set; }

	/// <summary>The fewest digits an exponent shows: its <c>0</c> placeholders, at least one.</summary>
	public int ExponentDigits { get; internal set; }

	/// <summary>How many placeholders a fraction's numerator has.</summary>
	public int NumeratorPlaceholders { get; internal set; }

	/// <summary>How many placeholders a fraction's denominator has: 0 for a number (<see cref="Denominator"/>).</summary>
	public int DenominatorPlaceholders { get; internal set; }

	/// <summary>A fraction's denominator when it is a number (<c># ?/8</c>), else 0.</summary>
	public int Denominator { get; internal set; }

	/// <summary>A fraction's numerator's placeholders' characters, in order: <c># ?#/??</c> has <c>?#</c>.</summary>
	public string NumeratorKinds { get; internal set; } = "";

	/// <summary>A fraction's denominator's placeholders' characters, in order: empty for a number.</summary>
	public string DenominatorKinds { get; internal set; } = "";

	/// <summary>
	/// The position of the first token after a fraction's whole number's last placeholder: 0 without a whole number. The
	/// tokens from there to <see cref="FractionEnd"/> are the fraction, which a zero fraction after a whole number blanks.
	/// </summary>
	public int WholeEnd { get; internal set; }

	/// <summary>The position of a fraction's numerator's first placeholder: the literals from <see cref="WholeEnd"/> to it are between the whole number and the fraction.</summary>
	public int NumeratorStart { get; internal set; }

	/// <summary>The position of the first token after a fraction's denominator.</summary>
	public int FractionEnd { get; internal set; }

	/// <summary>Whether a time shows 12 hours, with <c>AM/PM</c> or <c>A/P</c>.</summary>
	public bool TwelveHour { get; internal set; }

	/// <summary>Whether a time shows seconds, or fractions of one.</summary>
	public bool ShowsSeconds { get; internal set; }

	/// <summary>The most digits of fractions of a second it shows.</summary>
	public int SubSecondDigits { get; internal set; }

	/// <summary>Whether its only date or time token is one elapsed time, which alone shows any serial, a negative one too.</summary>
	public bool ElapsedAlone { get; internal set; }

	/// <summary>How many <c>@</c> a text section has.</summary>
	public int Ats { get; internal set; }

	/// <summary>Whether the section has any token or bracket: one of nothing, or of a locale alone, shows nothing.</summary>
	public bool IsEmpty => Tokens.Length == 0 && !HasColor && !HasDBNum && Condition == ConditionOperator.None && System == SystemFormat.None;

	/// <summary>Whether its condition takes a number.</summary>
	/// <param name="value">The number.</param>
	/// <returns>Whether it does; true without a condition.</returns>
	public bool Takes(double value) => Condition switch
	{
		ConditionOperator.Equal => value == Operand,
		ConditionOperator.NotEqual => value != Operand,
		ConditionOperator.Less => value < Operand,
		ConditionOperator.LessOrEqual => value <= Operand,
		ConditionOperator.Greater => value > Operand,
		ConditionOperator.GreaterOrEqual => value >= Operand,
		_ => true,
	};
}
