using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using ClosedXML.Excel;
using CsvHelper.Excel.Corpus;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Phase 7's templates in the written set (<c>WrittenTemplates.cs</c>) against Excel 365's answers about them, by the design:
/// each column's validation, prompt and title, from the row below the header to the last, and none above it or past the
/// header's columns; the cells below the header unlocked, and the rest locked; the protection, what it allows and its
/// password; the very hidden lists sheet. And the import of each copy that Excel filled in: each probe text reads as the set
/// says, and Excel took each the import reads and refused each it refuses, but where listed. GridCraft judges and fills the
/// same texts, and LibreOffice keeps the templates and saves Excel's copies again, their differences listed.
/// </summary>
/// <param name="output">The tests' output.</param>
public class TemplateAnswersTests(ITestOutputHelper output)
{
	private const string EnumCase = "the import reads an enum's name in any case; Excel's inline list matches in case (decision 7)";

	private const string IsoDate = "the import reads ISO 8601's T between a date and a time, which Excel takes as text, refused by its date validation";

	private const string BooleanWords = "the import reads yes and the number 1 as true; Excel's TRUE,FALSE list refuses them";

	private const string NegativeDuration = "the import reads -1:00 as a negative duration; Excel takes it as text, refused by its time validation";

	private const string LongDigits = "the import reads a long of 16 digits or more, as Excel stores it; the validation takes 15 (decision 3)";

	private const string StringList = "a string takes any text: the import doesn't check a column's list (decision 7)";

	private const string Validated = "the member's [CsvValidate] refuses what Excel's whole-number validation takes (decision 7)";

	private const string NoValidation = "the member's type has no validation (the design's table), and its parse refuses the text";

	private const string DottedText = "the import reads a [CsvFormat] date's own text, which Excel takes as text, refused by its date validation";

	private const string LibreOfficeExponent = "LibreOffice writes a number's exponent in three digits (1E+029), which core's decimal refuses with .NET's message, where Excel's 17 digits (9.9999999999999991E+28) get core's";

	private const string GridCraftCase = "GridCraft matches an inline list ignoring case, where Excel matches in case (Phase 7's design)";

	/// <summary>Where Excel's verdict on a text typed differs from what the import reads of it, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] FilledDifferences =
	[
		("template.xlsx", "Orders!B11", EnumCase),
		("template.xlsx", "Orders!C17", IsoDate),
		("template.xlsx", "Orders!E28", BooleanWords),
		("template.xlsx", "Orders!E29", BooleanWords),
		("template.xlsx", "Orders!F33", NegativeDuration),
		("template.xlsx", "Orders!H42", LongDigits),
		("template.xlsx", "Orders!H43", LongDigits),
		("template.xlsx", "Orders!J50", StringList),
		("template.xlsx", "Orders!J51", StringList),
		("template.xlsx", "Orders!K54", Validated),
		("template-types.xlsx", "Types!D13", NoValidation),
		("template-types.xlsx", "Types!R55", NoValidation),
		("template-types.xlsx", "Types!U63", NoValidation),
		("template-types.xlsx", "Types!X71", DottedText),
		("template-types-net.xlsx", "Types!A4", IsoDate),
		("template-lists.xlsx", "Lists!A3", StringList),
		("template-lists.xlsx", "Lists!A4", StringList),
		("template-lists.xlsx", "Lists!B7", StringList),
		("template-lists.xlsx", "Lists!C11", StringList),
		("template-lists.xlsx", "Lists!D14", StringList),
		("template-lists.xlsx", "Lists!E18", StringList),
		("template-lists.xlsx", "Lists!F21", StringList),
		("template-lists.xlsx", "Lists!G24", StringList),
		("template-prefilled.xlsx", "Orders!H5", LongDigits),
	];

	/// <summary>Where GridCraft judges a text otherwise than Excel, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] GridCraftDifferences =
	[
		("template.xlsx", "Orders!B11", GridCraftCase),
		("template.xlsx", "Orders!J50", GridCraftCase),
		("template-lists.xlsx", "Lists!A3", GridCraftCase),
	];

	/// <summary>Where the import of GridCraft's filled copy differs from what the set says the import reads, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] GridCraftFilledDifferences =
	[
		("template.xlsx", "Orders!H43", "GridCraft rounds a typed number of 18 digits to 15 (1.23456789012346E+17), where Excel cuts it (1.23456789012345E+17)"),
	];

	/// <summary>Where the import of LibreOffice's copy of Excel's filled copy says otherwise than of Excel's, and why.</summary>
	private static readonly (string File, string Cell, string Reason)[] LibreOfficeFilledDifferences =
	[
		("template.xlsx", "Orders!D22", LibreOfficeExponent),
		("template-types.xlsx", "Types!O46", LibreOfficeExponent),
	];

	/// <summary>The names of Excel's validation types (<c>xlDVType</c>), as the descriptions write them.</summary>
	private static readonly string[] Kinds = ["any", "whole", "decimal", "list", "date", "time", "textLength"];

	/// <summary>The written set's workbooks that the corpus tool fills: Phase 7's templates.</summary>
	public static TheoryData<string> Templates
	{
		get
		{
			var data = new TheoryData<string>();
			foreach (var workbook in Written.Workbooks.Where(w => w.Sheets.Any(s => s.Fill.Count > 0)))
			{
				data.Add(workbook.Name);
			}

			return data;
		}
	}

	[Fact]
	public void AnswersForEveryTemplate()
	{
		var templates = Written.Workbooks.Where(w => w.Sheets.Any(s => s.Fill.Count > 0)).ToList();
#if NET
		Assert.Equal(5, templates.Count);
#else
		Assert.Equal(4, templates.Count);
#endif
		foreach (var workbook in templates)
		{
			var answers = WrittenAnswers.Load(workbook.Name);

			// Excel was asked about each sheet's validations and locks, refused no good text, answered about every probe, and
			// saved its filled copy.
			Assert.All(answers.Sheets, s => Assert.NotNull(s.Validations));
			Assert.Empty(answers.GoodRefused);
			Assert.Equal(workbook.Sheets.SelectMany(s => s.Fill.Where(e => e.Probe).Select(e => (s.Name, e.Cell, e.Text))), answers.Filled.Select(f => (f.Sheet, f.Cell, f.Entered)));
			Assert.True(File.Exists(WrittenAnswers.FilledPath(workbook.Name)), workbook.Name);
		}

		// The other workbooks weren't asked.
		Assert.All(Written.Workbooks.Except(templates).Where(w => WrittenAnswers.Files.Contains(w.Name)), w => Assert.All(WrittenAnswers.Load(w.Name).Sheets, s => Assert.Null(s.Validations)));
	}

	[Theory]
	[MemberData(nameof(Templates))]
	public void ExcelValidatesAsDesigned(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		var differences = new List<string>();
		foreach (var sheet in answers!.Sheets)
		{
			var expected = ExpectedValidations(workbook, sheet.Name);
			foreach (var (reference, cell) in Sampled(sheet))
			{
				var actual = sheet.Validations!.TryGetValue(reference, out var v) ? v : null;
				var want = expected(cell);
				var described = actual is null ? "none" : Describe(actual.Type, actual.Formula1, actual.Formula2, actual.InputTitle, actual.InputMessage);
				if (described != (want ?? "none"))
				{
					differences.Add($"{SheetLocation.Quote(sheet.Name)}!{reference}: {want ?? "none"} designed, Excel's {described}");
				}

				// Excel's own alert, as Stop, over blanks too; a list's dropdown shows.
				if (actual is not null && (actual.AlertStyle, actual.IgnoreBlank, actual.InCellDropdown, actual.ShowInput, actual.ShowError, actual.ErrorTitle, actual.ErrorMessage) != (1, true, true, true, true, null, null))
				{
					differences.Add($"{SheetLocation.Quote(sheet.Name)}!{reference}: alert {actual.AlertStyle}, blank {actual.IgnoreBlank}, dropdown {actual.InCellDropdown}, input {actual.ShowInput}, error {actual.ShowError} [{actual.ErrorTitle}] [{actual.ErrorMessage}]");
				}

				// Between for ranges, equal for a text's length.
				if (actual is { Type: 1 or 2 or 4 or 5, Operator: not 1 } or { Type: 6, Operator: not 3 })
				{
					differences.Add($"{SheetLocation.Quote(sheet.Name)}!{reference}: operator {actual.Operator}");
				}
			}
		}

		Assert.True(differences.Count == 0, $"{name}:\n{string.Join("\n", differences)}");
	}

	[Theory]
	[MemberData(nameof(Templates))]
	public void ExcelLocksAndProtectsAsDesigned(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		var lists = WrittenOracle.ListsSheet(workbook);
		foreach (var sheet in answers!.Sheets)
		{
			var written = workbook.Sheets.SingleOrDefault(s => s.Names.Contains(sheet.Name));
			if (written is null)
			{
				// The lists sheet: very hidden, every cell locked, protected without a password, allowing selection alone.
				Assert.Equal(lists?.Name, sheet.Name);
				Assert.Equal(2, sheet.Visible);
				Assert.Empty(sheet.Unlocked);
				Assert.Equal(Allows(resize: false), Allowed(sheet));
				Assert.Equal((true, (bool?)null), sheet.Unprotect);

				continue;
			}

			// The header's columns below it unlocked, the title, the header and the columns past it locked.
			var settings = WrittenOracle.Settings(workbook, written);
			var header = settings.Title is null ? 1 : 2;
			var count = written.Columns.Count;
			Assert.Equal(-1, sheet.Visible);
			Assert.Equal(
				Sampled(sheet).Where(s => s.Cell.Row > header && s.Cell.Column < count).Select(s => s.Reference).OrderBy(r => r, StringComparer.Ordinal),
				sheet.Unlocked.OrderBy(r => r, StringComparer.Ordinal));

			// Excel's defaults, resizing columns and rows; a wrong password refused and the set's taken, or none asked for.
			Assert.Equal(Allows(resize: true), Allowed(sheet));
			Assert.Equal(settings.Password is null ? (true, (bool?)null) : (false, true), sheet.Unprotect);
		}
	}

	[Theory]
	[MemberData(nameof(Templates))]
	public void ExcelShowsATemplatesColumnsAtTheDefaultWidthAtLeast(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		foreach (var sheet in workbook.Sheets.Where(s => s.IsTemplate))
		{
			// Each header column as wide as Excel's default at least (Phase 7's step 5), so that a number typed shows whole.
			var widths = answers!.Sheets.Single(s => s.Name == sheet.Name).Widths;
			var least = WrittenOracle.ExcelWidth(ColumnWidths.Default, answers.Dpi);
			Assert.All(Enumerable.Range(0, sheet.Columns.Count), c => Assert.True(widths[c] >= least, $"{sheet.Name}!{CellReference.ColumnName(c)}: {widths[c]}, narrower than {least}"));
		}
	}

	[Theory]
	[MemberData(nameof(Templates))]
	public void ImportsWhatExcelFilled(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		var differences = new List<string>();
		foreach (var sheet in workbook.Sheets.Where(s => s.Fill.Count > 0))
		{
			using var stream = File.OpenRead(WrittenAnswers.FilledPath(name));
			var outcomes = Import(workbook, sheet, stream, out var errors);
			foreach (var probe in sheet.Fill.Where(e => e.Probe))
			{
				var location = $"{SheetLocation.Quote(sheet.Name)}!{probe.Cell}";
				var excel = answers!.Filled.Single(f => f.Sheet == sheet.Name && f.Cell == probe.Cell);

				// The import reads what the set says, whatever Excel made of the text.
				Assert.True(outcomes[probe.Cell] == probe.Reads, $"{location} '{probe.Text}': the import gives {outcomes[probe.Cell] ?? "an error"}, the set says {probe.Reads ?? "an error"} ({errors})");
				if (excel.Valid != (probe.Reads is not null))
				{
					differences.Add($"{location}: Excel {(excel.Valid ? "took" : "refused")} '{probe.Text}' ({excel.Type} {excel.Value2}), which the import {(probe.Reads is null ? "refuses" : "reads as " + probe.Reads)}");
				}
			}
		}

		AssertListed(name, differences, FilledDifferences);
	}

	[GridCraftTheory]
	[MemberData(nameof(Templates))]
	public void GridCraftJudgesAndFillsAsExcelDoes(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		var judged = new List<string>();
		var read = new List<string>();
		foreach (var sheet in workbook.Sheets.Where(s => s.Fill.Count > 0))
		{
			// GridCraft judges every text as it types it into the template, as written, and saves its copy.
			var saved = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.GridCraft.{Guid.NewGuid():N}.xlsx");
			try
			{
				var verdicts = GridCraft.Fill(GridCraft.WrittenPath(workbook), [.. sheet.Fill.Select(e => (e.Cell, e.Text))], saved);
				for (var i = 0; i < sheet.Fill.Count; i++)
				{
					var entry = sheet.Fill[i];
					var excel = entry.Probe ? answers!.Filled.Single(f => f.Sheet == sheet.Name && f.Cell == entry.Cell).Valid : true;
					if (verdicts[i] != excel)
					{
						judged.Add($"{SheetLocation.Quote(sheet.Name)}!{entry.Cell}: GridCraft {(verdicts[i] ? "took" : "refused")} '{entry.Text}', Excel {(excel ? "took" : "refused")} it");
					}
				}

				using var stream = File.OpenRead(saved);
				var outcomes = Import(workbook, sheet, stream, out _);
				foreach (var probe in sheet.Fill.Where(e => e.Probe && outcomes[e.Cell] != e.Reads))
				{
					read.Add($"{SheetLocation.Quote(sheet.Name)}!{probe.Cell}: the import of GridCraft's copy gives {outcomes[probe.Cell] ?? "an error"} for '{probe.Text}', of Excel's {probe.Reads ?? "an error"}");
				}
			}
			finally
			{
				File.Delete(saved);
			}
		}

		output.WriteLine(GridCraft.Version);
		AssertListed(name, judged, GridCraftDifferences);
		AssertListed(name, read, GridCraftFilledDifferences);
	}

	[LibreOfficeTheory]
	[MemberData(nameof(Templates))]
	public void ImportsWhatLibreOfficeSavedOfExcelsCopy(string name)
	{
		var (workbook, _) = WrittenAnswersTests.Comparable(name, output);
		var differences = new List<string>();
		foreach (var sheet in workbook.Sheets.Where(s => s.Fill.Count > 0))
		{
			using var excel = File.OpenRead(WrittenAnswers.FilledPath(name));
			using var libreOffice = File.OpenRead(LibreOffice.FilledAsXlsx(name));
			var fromExcel = Import(workbook, sheet, excel, out var excelErrors);
			var fromLibreOffice = Import(workbook, sheet, libreOffice, out var libreOfficeErrors);

			// The same records and the same cells at fault; and the same messages, but where listed.
			Assert.Equal(fromExcel.OrderBy(o => o.Key, StringComparer.Ordinal), fromLibreOffice.OrderBy(o => o.Key, StringComparer.Ordinal));
			Assert.Equal(excelErrors.Select(e => e.Location), libreOfficeErrors.Select(e => e.Location));
			differences.AddRange(excelErrors.Zip(libreOfficeErrors, (e, l) => (e, l)).Where(p => p.e.Message != p.l.Message).Select(p => $"{p.e.Location}: Excel's copy '{p.e.Message}', LibreOffice's '{p.l.Message}'"));
		}

		AssertListed(name, differences, LibreOfficeFilledDifferences);
	}

	[LibreOfficeTheory]
	[MemberData(nameof(Templates))]
	public void LibreOfficeKeepsTheValidationsAndLocks(string name)
	{
		var (workbook, answers) = WrittenAnswersTests.Comparable(name, output);
		var saved = File.ReadAllBytes(LibreOffice.WrittenLook(workbook));
		using var book = new XLWorkbook(new MemoryStream(saved));
		var extents = new List<string>();
		foreach (var sheet in answers!.Sheets)
		{
			var written = workbook.Sheets.SingleOrDefault(s => s.Names.Contains(sheet.Name));
			var kept = book.Worksheet(sheet.Name);
			if (written is null)
			{
				// The lists sheet, kept protected; LibreOffice makes a very hidden sheet hidden (Phase 7's design).
				Assert.Equal(XLWorksheetVisibility.Hidden, kept.Visibility);
				Assert.True(kept.IsProtected);

				continue;
			}

			// Each validation as designed, over the rows LibreOffice keeps it: from the row below the header to the rows it uses
			// and 1,000 more (Phase 7's design), where Excel's is to the last row.
			var settings = WrittenOracle.Settings(workbook, written);
			var header = settings.Title is null ? 1 : 2;
			var expected = ExpectedValidations(workbook, sheet.Name);
			var validations = LibreOfficeValidations(saved, workbook.Sheets.ToList().IndexOf(written) + 1);
			for (var c = 0; c <= written.Columns.Count; c++)
			{
				var letters = CellReference.ColumnName(c);
				var want = expected((header + 1, c)) ?? "none";
				var (described, first, last) = validations.TryGetValue(letters, out var kept1) ? kept1 : ("none", 0L, 0L);
				Assert.True(want == described, $"{sheet.Name}!{letters}: {want} designed, LibreOffice's {described}");
				if (described != "none")
				{
					Assert.Equal(header + 1, first);
					extents.Add(last.ToString(CultureInfo.InvariantCulture));
				}
			}

			// The cells below the header in its columns unlocked, the header and the column past it locked; the protection kept.
			Assert.True(kept.IsProtected);
			for (var c = 1; c <= written.Columns.Count + 1; c++)
			{
				Assert.True(kept.Cell(header, c).Style.Protection.Locked, $"{sheet.Name}: header cell {c} unlocked");
				Assert.Equal(c > written.Columns.Count, kept.Cell(header + 1, c).Style.Protection.Locked);
			}
		}

		// The extent LibreOffice gives every validation: the rows it uses and 1,000 more.
		output.WriteLine($"{name}: validations to rows {string.Join(", ", extents.Distinct())}");
		Assert.All(extents, e => Assert.NotEqual("1048576", e));
	}

	/// <summary>The cells Excel was asked about: rows 1 to the one below the used range, and the last; columns A to the one past the used range's last.</summary>
	/// <param name="sheet">The sheet's answers.</param>
	/// <returns>Each cell's reference and its row and 0-based column.</returns>
	private static IEnumerable<(string Reference, (long Row, int Column) Cell)> Sampled(WrittenAnswerSheet sheet)
	{
		var rows = Enumerable.Range(1, (int)sheet.Used.Row + 1).Select(r => (long)r).Append(1_048_576L);
		foreach (var row in rows)
		{
			for (var column = 0; column <= sheet.Used.Column + 1; column++)
			{
				yield return (CellReference.ColumnName(column) + row.ToString(CultureInfo.InvariantCulture), (row, column));
			}
		}
	}

	/// <summary>
	/// The validation the design gives each cell of a sheet, described as <see cref="Describe"/> describes Excel's, or
	/// <see langword="null"/> for none: a column's from the row below the header to the last, none above it or past the
	/// header's columns, and none on the lists sheet.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="name">The sheet's name.</param>
	/// <returns>The cell's validation, by its row and 0-based column.</returns>
	private static Func<(long Row, int Column), string?> ExpectedValidations(WrittenWorkbook workbook, string name)
	{
		var sheet = workbook.Sheets.SingleOrDefault(s => s.Names.Contains(name));
		if (sheet is null)
		{
			return _ => null;
		}

		var settings = WrittenOracle.Settings(workbook, sheet);
		var header = settings.Title is null ? 1 : 2;
		var options = WrittenOracle.ColumnOptions(workbook, sheet);
		var lists = WrittenOracle.ListsSheet(workbook)?.Lists ?? [];
		var columns = sheet.Columns.Select((c, i) => Designed(c, options[i], settings, workbook.Options, lists)).ToList();

		return cell => cell.Row > header && cell.Column < columns.Count ? columns[cell.Column] : null;
	}

	/// <summary>
	/// A column's validation by the design: its list in place of its type's, inline or by its name in the lists sheet; its
	/// member's type's, by the design's table, on a sheet that validates; its prompt, titled by its header cut to 32
	/// characters, alone a validation of any value.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="options">Its options.</param>
	/// <param name="settings">The sheet's settings.</param>
	/// <param name="workbook">The workbook's options.</param>
	/// <param name="lists">The lists in the lists sheet, in order.</param>
	/// <returns>The description, or <see langword="null"/> for none.</returns>
	private static string? Designed(WrittenColumn column, ExcelColumnOptions? options, ExcelSheetOptions settings, ExcelWriterOptions workbook, List<IReadOnlyList<string>> lists)
	{
		(int Type, string? Formula1, string? Formula2)? validation = null;
		if (options?.List is { } values)
		{
			var distinct = values.Distinct(StringComparer.Ordinal).ToList();
			validation = (3, WrittenOracle.Inline(distinct) ? string.Join(",", distinct) : $"=_CsvHelperList{lists.FindIndex(l => l.SequenceEqual(distinct)) + 1}", null);
		}
		else if (settings.Validate && column.Member is { } member)
		{
			validation = TypeValidation(member, workbook);
		}

		string? title = null;
		if (options?.Prompt is not null)
		{
			validation ??= (0, null, null);
			title = column.Name.Length <= 32 ? column.Name : column.Name.Substring(0, char.IsHighSurrogate(column.Name[31]) ? 31 : 32);
		}

		return validation is { } v ? Describe(v.Type, v.Formula1, v.Formula2, title, options?.Prompt) : null;
	}

	/// <summary>A member's type's validation, as the design's table gives it, in Excel's terms: its type, and its formulas.</summary>
	/// <param name="member">The member.</param>
	/// <param name="options">The workbook's options.</param>
	/// <returns>The validation, or <see langword="null"/> for none.</returns>
	private static (int, string?, string?)? TypeValidation(PropertyInfo member, ExcelWriterOptions options)
	{
		var type = Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType;
		if (type == typeof(bool))
		{
			return member.GetCustomAttribute<CsvBooleanValuesAttribute>() is { } values
				? (3, $"{values.TrueValues[0].Trim()},{values.FalseValues[0].Trim()}", null)
				: (3, "TRUE,FALSE", null);
		}

		if (type.IsEnum)
		{
			return type.GetCustomAttribute<FlagsAttribute>() is not null
				? null
				: (3, string.Join(",", type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.GetCustomAttribute<CsvEnumNameAttribute>()?.Names.FirstOrDefault() ?? f.Name).Distinct(StringComparer.Ordinal)), null);
		}

		var whole = new Dictionary<Type, (string, string)>
		{
			[typeof(sbyte)] = ("-128", "127"), [typeof(byte)] = ("0", "255"), [typeof(short)] = ("-32768", "32767"), [typeof(ushort)] = ("0", "65535"),
			[typeof(int)] = ("-2147483648", "2147483647"), [typeof(uint)] = ("0", "4294967295"),
			[typeof(long)] = ("-999999999999999", "999999999999999"), [typeof(ulong)] = ("0", "999999999999999"),
		};
		if (whole.TryGetValue(type, out var bounds))
		{
			return (1, bounds.Item1, bounds.Item2);
		}

		var numbers = new Dictionary<Type, string> { [typeof(float)] = "3.40282346638529E+38", [typeof(double)] = "9.99999999999999E+307", [typeof(decimal)] = "7.92281625142643E+28" };
		if (numbers.TryGetValue(type, out var largest))
		{
			return (2, "-" + largest, largest);
		}

		const string lastSerial = "2958465.9999999884";
		if (type == typeof(DateTime) || (type == typeof(DateTimeOffset) && options.DateTimeOffsets != ExcelDateTimeOffsets.Text))
		{
			return (4, "1", lastSerial);
		}

		if (type == typeof(TimeSpan))
		{
			return (5, "0", lastSerial);
		}

#if NET
		if (type == typeof(DateOnly))
		{
			return (4, "1", lastSerial);
		}

		if (type == typeof(TimeOnly))
		{
			return (5, "0", "0.99999998842592586");
		}
#endif

		return type == typeof(char) ? (6, "1", null) : null;
	}

	/// <summary>
	/// A validation as the tests compare it: its kind, its bounds (numbers in their shortest form, dates and times as their
	/// serials' whole days, since Excel's text of a date's bound shows no fraction of a second), a list's values or name,
	/// its title and its prompt.
	/// </summary>
	/// <param name="type">Its <c>xlDVType</c>.</param>
	/// <param name="formula1">Its first formula, as Excel or the XML has it.</param>
	/// <param name="formula2">Its second formula.</param>
	/// <param name="title">Its prompt's title.</param>
	/// <param name="prompt">Its prompt.</param>
	/// <returns>The description.</returns>
	private static string Describe(int type, string? formula1, string? formula2, string? title, string? prompt)
	{
		var parts = new List<string> { Kinds[type] };
		switch (type)
		{
			case 1 or 2:
				parts.Add(Number(formula1));
				parts.Add(Number(formula2));

				break;
			case 6:
				parts.Add(Number(formula1));

				break;
			case 4 or 5:
				parts.Add(Day(formula1));
				parts.Add(Day(formula2));

				break;
			case 3:
				parts.Add(List(formula1));

				break;
		}

		if (title is not null)
		{
			parts.Add("title=" + title);
		}

		if (prompt is not null)
		{
			parts.Add("prompt=" + prompt.Replace("\r\n", "\n"));
		}

		return string.Join(" ", parts);
	}

	/// <summary>A bound's number in its shortest form.</summary>
	/// <param name="formula">The formula, with or without its <c>=</c>.</param>
	/// <returns>The number.</returns>
	private static string Number(string? formula) =>
		double.Parse(formula!.TrimStart('='), NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);

	/// <summary>
	/// A date's or time's bound as its serial's whole days: a serial, or Excel's text of the date or time, in en-US, as Excel
	/// numbers it: before 1900-03-01, a day less than an OLE date, since Excel's 1900 has a 29 February.
	/// </summary>
	/// <param name="formula">The formula.</param>
	/// <returns>The whole days.</returns>
	private static string Day(string? formula)
	{
		var text = formula!.TrimStart('=');
		var enUs = CultureInfo.GetCultureInfo("en-US");
		double serial;
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
		{
			serial = number;
		}
		else if (DateTime.Parse(text, enUs, DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces) is { Year: 1 } time)
		{
			serial = time.TimeOfDay.TotalDays;
		}
		else
		{
			var date = DateTime.Parse(text, enUs, DateTimeStyles.AllowWhiteSpaces);
			serial = date.ToOADate() - (date < new DateTime(1900, 3, 1) ? 1 : 0);
		}

		return Math.Floor(serial).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>A list's values joined by commas, or its name with <c>=</c>, from Excel's text or the XML's (quoted, its quotes doubled).</summary>
	/// <param name="formula">The formula.</param>
	/// <returns>The list.</returns>
	private static string List(string? formula) =>
		formula!.StartsWith("\"", StringComparison.Ordinal) ? formula.Substring(1, formula.Length - 2).Replace("\"\"", "\"")
			: formula.StartsWith("=", StringComparison.Ordinal) || formula.Contains(',') || !formula.StartsWith("_CsvHelperList", StringComparison.Ordinal) ? formula
			: "=" + formula;

	/// <summary>What a protected sheet allows, as the design gives it: Excel's defaults, and resizing columns and rows.</summary>
	/// <param name="resize">Whether it allows resizing; the lists sheet doesn't.</param>
	/// <returns>Each allowance, by the answers' names.</returns>
	private static Dictionary<string, string> Allows(bool resize) => new()
	{
		["objects"] = "true", ["scenarios"] = "true", ["selection"] = "0", ["formatCells"] = "false", ["formatColumns"] = resize ? "true" : "false",
		["formatRows"] = resize ? "true" : "false", ["insertColumns"] = "false", ["insertRows"] = "false", ["insertLinks"] = "false",
		["deleteColumns"] = "false", ["deleteRows"] = "false", ["sort"] = "false", ["filter"] = "false", ["pivotTables"] = "false",
	};

	/// <summary>What a sheet's protection allows, as Excel answered.</summary>
	/// <param name="sheet">The sheet's answers.</param>
	/// <returns>Each allowance.</returns>
	private static Dictionary<string, string> Allowed(WrittenAnswerSheet sheet) => sheet.Protection!.ToDictionary(p => p.Key, p => p.Value);

	/// <summary>
	/// Imports a filled copy of a template's sheet, and gives what it read of each probe's cell: its member's value as the
	/// set shows it, or <see langword="null"/> for an error at the cell. Each record's row is found from the rows written
	/// and filled, in order, without those with an error.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="stream">The copy.</param>
	/// <param name="errors">The import's errors, for messages.</param>
	/// <returns>Each probe's cell's reading.</returns>
	private static Dictionary<string, string?> Import(WrittenWorkbook workbook, WrittenSheet sheet, Stream stream, out IReadOnlyList<ExcelImportError> errors)
	{
		var header = WrittenOracle.Settings(workbook, sheet).Title is null ? 1 : 2;
		var result = sheet.Import!(stream, new ExcelReaderOptions { Sheet = sheet.Name, HeaderRow = ExcelHeaderRow.At(header) });
		errors = result.Errors;
		Assert.True(result.Complete);
		var rows = Enumerable.Range(header + 1, sheet.Rows.Count - 1).Select(r => (long)r)
			.Concat(sheet.Fill.Select(e => WrittenAnswers.Row(e.Cell)))
			.Distinct()
			.OrderBy(r => r)
			.ToList();
		var failed = result.Errors.Select(e => e.Row).ToHashSet();
		var records = new Dictionary<long, object>();
		var next = 0;
		foreach (var row in rows.Where(r => !failed.Contains(r)))
		{
			records[row] = result.Records[next++];
		}

		Assert.Equal(result.Records.Count, next);
		var probes = sheet.Fill.Where(e => e.Probe).ToList();
		var located = probes.Select(p => $"{SheetLocation.Quote(sheet.Name)}!{p.Cell}").ToHashSet(StringComparer.Ordinal);
		Assert.All(result.Errors, e => Assert.True(located.Contains(e.Location!), $"{e.Location}: an error at a cell no probe is in: {e.Message}"));
		var readings = new Dictionary<string, string?>(StringComparer.Ordinal);
		foreach (var probe in probes)
		{
			var row = WrittenAnswers.Row(probe.Cell);
			var column = sheet.Columns[WrittenAnswers.Column(probe.Cell)].Name;
			readings[probe.Cell] = result.Errors.Any(e => e.Location == $"{SheetLocation.Quote(sheet.Name)}!{probe.Cell}")
				? null
				: Written.Show(records[row].GetType().GetProperty(column)!.GetValue(records[row]));
		}

		return readings;
	}

	/// <summary>
	/// Each validation that LibreOffice kept in a sheet of its <c>.xlsx</c>, by the column it covers: described as Excel's,
	/// and the first and last rows it covers.
	/// </summary>
	/// <param name="workbook">LibreOffice's file.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <returns>The validations.</returns>
	private static Dictionary<string, (string Described, long First, long Last)> LibreOfficeValidations(byte[] workbook, int sheet)
	{
		XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
		using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
		using var part = archive.GetEntry($"xl/worksheets/sheet{sheet}.xml")!.Open();
		var xml = XDocument.Load(part);
		var types = new Dictionary<string, int> { ["none"] = 0, ["whole"] = 1, ["decimal"] = 2, ["list"] = 3, ["date"] = 4, ["time"] = 5, ["textLength"] = 6 };
		var result = new Dictionary<string, (string, long, long)>(StringComparer.Ordinal);
		foreach (var validation in xml.Descendants(main + "dataValidation"))
		{
			var described = Describe(
				types[(string?)validation.Attribute("type") ?? "none"], validation.Element(main + "formula1")?.Value, validation.Element(main + "formula2")?.Value,
				(string?)validation.Attribute("promptTitle"), (string?)validation.Attribute("prompt"));
			foreach (var range in ((string)validation.Attribute("sqref")!).Split(' '))
			{
				var ends = range.Split(':');
				var (first, column) = WrittenAnswers.Position(ends[0]);
				var (last, _) = WrittenAnswers.Position(ends[ends.Length - 1]);
				result[CellReference.ColumnName(column)] = (described, first, last);
			}
		}

		return result;
	}

	/// <summary>Fails with the differences not listed, and the listed ones not found.</summary>
	/// <param name="name">The workbook's name.</param>
	/// <param name="differences">The differences, each starting with its cell.</param>
	/// <param name="list">The listed differences.</param>
	private static void AssertListed(string name, List<string> differences, (string File, string Cell, string Reason)[] list)
	{
		var listed = list.Where(d => d.File == name).ToList();
		var unexplained = differences.Where(d => !listed.Any(l => d.StartsWith(l.Cell + ":", StringComparison.Ordinal))).ToList();
		var unseen = listed.Where(l => !differences.Any(d => d.StartsWith(l.Cell + ":", StringComparison.Ordinal))).Select(l => l.Cell).ToList();
		Assert.True(unexplained.Count == 0 && unseen.Count == 0, $"{name}: not listed:\n{string.Join("\n", unexplained)}\nlisted but not found: [{string.Join(", ", unseen)}]");
	}
}
