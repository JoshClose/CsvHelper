namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record type with a sheet's attribute, and columns' attributes.</summary>
[CsvRecord]
[ExcelSheet(Title = "Orders", FreezeColumns = 1, AutoFit = 5, MaxAutoFitWidth = 40)]
public partial class SheetedOrder
{
	/// <summary>A width and a format.</summary>
	[ExcelColumn(Width = 12, Format = "#,##0.00")]
	public decimal Amount { get; set; }

	/// <summary>Hidden.</summary>
	[ExcelColumn(Hidden = true)]
	public string Note { get; set; } = "";

	/// <summary>No attribute.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A base type with a sheet's attribute, which isn't a record type.</summary>
[ExcelSheet(AutoFilter = true, BoldHeader = false)]
public class SheetedBase
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A record type whose base type has the sheet's attribute.</summary>
[CsvRecord]
public partial class SheetedDerived : SheetedBase
{
	/// <summary>A number.</summary>
	public int Count { get; set; }
}

/// <summary>A type that isn't a record type, with a sheet's attribute.</summary>
[ExcelSheet(Title = "Plain")]
public class PlainSheeted
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A sheet's attribute with a table.</summary>
[CsvRecord]
[ExcelSheet(Table = true, TableName = "Orders", TableStyle = "tablestylelight9")]
public partial class TabledOrder
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A sheet's attribute out of range.</summary>
[CsvRecord]
[ExcelSheet(FreezeColumns = -1)]
public partial class BadSheet
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A table's name without a table.</summary>
[CsvRecord]
[ExcelSheet(TableName = "Orders")]
public partial class TableNameWithoutTable
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A table's name Excel refuses.</summary>
[CsvRecord]
[ExcelSheet(Table = true, TableName = "A1")]
public partial class BadTableName
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";
}

/// <summary>A width past Excel's widest.</summary>
[CsvRecord]
public partial class BadColumnWidth
{
	/// <summary>Too wide.</summary>
	[ExcelColumn(Width = 300)]
	public int Count { get; set; }
}

/// <summary>A format Excel refuses.</summary>
[CsvRecord]
public partial class BadColumnFormat
{
	/// <summary>A letter outside quotes.</summary>
	[ExcelColumn(Format = "0.00 kg")]
	public int Count { get; set; }
}

/// <summary>A column's attribute on a member that nests a record.</summary>
[CsvRecord]
public partial class ExcelColumnOnNested
{
	/// <summary>A nested record, whose members have the columns.</summary>
	[ExcelColumn(Width = 10)]
	public FormattedAddress Address { get; set; } = new();
}

/// <summary>Columns' attributes on constructor parameters, the properties they set, a collection and a converter's member.</summary>
/// <param name="Name">The attribute on the parameter.</param>
/// <param name="Note">The attribute on the property.</param>
[CsvRecord]
public partial record ColumnedPosition([ExcelColumn(Width = 20)] string Name, [property: ExcelColumn(Wrap = true)] string Note)
{
	/// <summary>Two columns from index 2.</summary>
	[CsvColumn("Day", Index = 2, EndIndex = 3)]
	[ExcelColumn(Format = "0.0")]
	public List<int> Days { get; init; } = [];

	/// <summary>Written by its converter, in one column.</summary>
	[CsvColumn(Index = 4)]
	[CsvConverter(typeof(TicksConverter))]
	[ExcelColumn(Width = 25)]
	public DateTime Converted { get; init; }
}

/// <summary>
/// A sheet's settings: where they come from (the options given, the record type's <see cref="ExcelSheetAttribute"/>, the
/// workbook's <see cref="ExcelWriterOptions.Sheets"/>), each taken whole; their checks; and the columns' options by the
/// header's names.
/// </summary>
public class SheetSettingsTests
{
	/// <summary>Options that differ from the defaults in every setting.</summary>
	private static readonly ExcelSheetOptions Given = new()
	{
		Title = "Given",
		BoldHeader = false,
		FreezeHeader = false,
		FreezeColumns = 2,
		AutoFilter = true,
		AutoFit = 0,
		MaxAutoFitWidth = 20,
		Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { Wrap = true } },
	};

	[Fact]
	public void DefaultsToABoldFrozenAutoFittedHeader()
	{
		var settings = SheetSettings.Resolve(null, null, null, ExcelWriterOptions.Default);

		Assert.Same(ExcelSheetOptions.Default, settings.Sheet);
		Assert.Null(settings.Title);
		Assert.True(settings.BoldHeader);
		Assert.True(settings.FreezeHeader);
		Assert.Equal(0, settings.FreezeColumns);
		Assert.False(settings.AutoFilter);
		Assert.Null(settings.Table);
		Assert.Equal(100, settings.AutoFit);
		Assert.Equal(60, settings.MaxAutoFitWidth);
		Assert.Null(settings.ColumnsFor(["A", "B"]));
		Assert.Equal(ExcelLinks.Web, ExcelWriterOptions.Default.Links);
		Assert.Equal(new ExcelTableOptions { Name = null, Style = "TableStyleMedium2", BandedRows = true, FilterButtons = true }, new ExcelTableOptions());
		Assert.Equal(new ExcelColumnOptions { Width = null, Format = null, Wrap = false, Hidden = false }, new ExcelColumnOptions());
	}

	[Fact]
	public void TakesTheOptionsGivenWhole()
	{
		var members = MemberFormats.For<SheetedOrder>();
		var writer = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFit = 7 } };

		var settings = SheetSettings.Resolve(Given, typeof(SheetedOrder), members, writer);

		// Not merged with the attribute's (Title "Orders", AutoFit 5) or the workbook's (AutoFit 7).
		Assert.Same(Given, settings.Sheet);
		Assert.Equal("Given", settings.Title);
		Assert.False(settings.BoldHeader);
		Assert.False(settings.FreezeHeader);
		Assert.Equal(2, settings.FreezeColumns);
		Assert.True(settings.AutoFilter);
		Assert.Equal(0, settings.AutoFit);
		Assert.Equal(20, settings.MaxAutoFitWidth);
	}

	[Fact]
	public void TakesTheRecordTypesAttributeWholeWithoutOptions()
	{
		var writer = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFilter = true, Title = "Workbook's" } };

		var settings = SheetSettings.Resolve(null, typeof(SheetedOrder), MemberFormats.For<SheetedOrder>(), writer);

		Assert.Equal("Orders", settings.Title);
		Assert.Equal(1, settings.FreezeColumns);
		Assert.Equal(5, settings.AutoFit);
		Assert.Equal(40, settings.MaxAutoFitWidth);
		// The workbook's filter isn't merged in, and the attribute's defaults are ExcelSheetOptions'.
		Assert.False(settings.AutoFilter);
		Assert.True(settings.BoldHeader);
		Assert.True(settings.FreezeHeader);
		Assert.Null(settings.Table);
	}

	[Fact]
	public void TakesTheWorkbooksOptionsWithoutEither()
	{
		var sheets = new ExcelSheetOptions { AutoFilter = true, AutoFit = 7 };

		var settings = SheetSettings.Resolve(null, typeof(WrittenText), MemberFormats.For<WrittenText>(), new ExcelWriterOptions { Sheets = sheets });

		Assert.Same(sheets, settings.Sheet);
		Assert.True(settings.AutoFilter);
		Assert.Equal(7, settings.AutoFit);
	}

	[Fact]
	public void ReadsTheAttributeFromABaseTypeAndFromTypesThatArentRecords()
	{
		var derived = SheetSettings.Resolve(null, typeof(SheetedDerived), MemberFormats.For<SheetedDerived>(), ExcelWriterOptions.Default);
		var plain = MemberFormats.For<PlainSheeted>();

		Assert.True(derived.AutoFilter);
		Assert.False(derived.BoldHeader);
		Assert.NotNull(plain);
		Assert.Equal("Plain", plain.Sheet!.Title);
		Assert.Empty(plain.Entries);
		Assert.False(plain.HasFormats);
	}

	[Fact]
	public void ReadsTheAttributesTable()
	{
		var settings = SheetSettings.Resolve(null, typeof(TabledOrder), MemberFormats.For<TabledOrder>(), ExcelWriterOptions.Default);

		Assert.Equal(new ExcelTableOptions { Name = "Orders", Style = "tablestylelight9" }, settings.Table);
	}

	[Fact]
	public void LeavesTheHeaderPlainAndUnfilteredInATable()
	{
		var styled = Settings(new ExcelSheetOptions { AutoFilter = true, Table = new ExcelTableOptions() });
		var unstyled = Settings(new ExcelSheetOptions { Table = new ExcelTableOptions { Style = null } });

		// A table's style sets its header, and its buttons are the filter, which Excel refuses beside a sheet's own.
		Assert.False(styled.BoldHeader);
		Assert.False(styled.AutoFilter);
		Assert.True(unstyled.BoldHeader);
	}

	[Fact]
	public void HasNoHeaderToStyleWithoutHasHeader()
	{
		var writer = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };

		var settings = SheetSettings.Resolve(new ExcelSheetOptions { FreezeColumns = 1 }, null, null, writer);

		Assert.False(settings.BoldHeader);
		Assert.False(settings.FreezeHeader);
		Assert.Equal(1, settings.FreezeColumns);
		Assert.False(settings.HasHeader);
	}

	[Fact]
	public void RefusesAFilterOrATableWithoutHasHeader()
	{
		var writer = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };

		var filter = Assert.Throws<ArgumentException>(() => SheetSettings.Resolve(new ExcelSheetOptions { AutoFilter = true }, null, null, writer));
		var table = Assert.Throws<ArgumentException>(() => SheetSettings.Resolve(new ExcelSheetOptions { Table = new ExcelTableOptions() }, null, null, writer));
		var attribute = Assert.Throws<InvalidOperationException>(() => SheetSettings.Resolve(null, typeof(SheetedDerived), MemberFormats.For<SheetedDerived>(), writer));
		var workbook = Assert.Throws<ArgumentException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), writer with { Sheets = new ExcelSheetOptions { AutoFilter = true } }));

		// .NET Framework puts the parameter's name on a line of its own, .NET in parentheses after the message.
		Assert.StartsWith("ExcelSheetOptions: AutoFilter needs a header, and CsvWriterOptions.HasHeader is off.", filter.Message);
		Assert.Equal("options", filter.ParamName);
		Assert.StartsWith("ExcelSheetOptions: A table needs a header, and CsvWriterOptions.HasHeader is off.", table.Message);
		Assert.Equal("options", table.ParamName);
		Assert.Equal("The [ExcelSheet] of SheetedDerived: AutoFilter needs a header, and CsvWriterOptions.HasHeader is off.", attribute.Message);
		Assert.StartsWith("ExcelWriterOptions.Sheets: AutoFilter needs a header, and CsvWriterOptions.HasHeader is off.", workbook.Message);
		Assert.Equal("options", workbook.ParamName);
	}

	/// <summary>Options Excel wouldn't open, and what each one's message says.</summary>
	public static TheoryData<ExcelSheetOptions, string> Refused => new()
	{
		{ new ExcelSheetOptions { Title = "" }, "The title has 0 characters, and a title has 1 to 32,767: use null for none." },
		{ new ExcelSheetOptions { Title = new string('t', 32768) }, "The title has 32768 characters" },
		{ new ExcelSheetOptions { FreezeColumns = -1 }, "FreezeColumns is -1, and is 0 to 16,383." },
		{ new ExcelSheetOptions { FreezeColumns = 16384 }, "FreezeColumns is 16384" },
		{ new ExcelSheetOptions { AutoFit = -1 }, "AutoFit is -1, and is 0 (none) to 1,048,575 records." },
		{ new ExcelSheetOptions { AutoFit = 1048576 }, "AutoFit is 1048576" },
		{ new ExcelSheetOptions { MaxAutoFitWidth = 0.5 }, "MaxAutoFitWidth is 0.5, and is 1 to 255 characters." },
		{ new ExcelSheetOptions { MaxAutoFitWidth = 255.5 }, "MaxAutoFitWidth is 255.5" },
		{ new ExcelSheetOptions { MaxAutoFitWidth = double.NaN }, "MaxAutoFitWidth is NaN" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Style = "TableStyleMedium29" } }, "The table has the style \"TableStyleMedium29\", which isn't one of Excel's built-in table styles" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Style = "TableStyleLight0" } }, "\"TableStyleLight0\"" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Style = "TableStyleDark01" } }, "\"TableStyleDark01\"" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Style = "Custom" } }, "\"Custom\"" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Style = "" } }, "\"\"" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Name = "My Orders" } }, "The table has the name \"My Orders\", which has U+0020" },
		{ new ExcelSheetOptions { Table = new ExcelTableOptions { Name = "R1C1" } }, "a row or column reference" },
		{ Column(new ExcelColumnOptions { Width = -1 }), "The column \"Amount\" has the width -1, and Excel's widths are 0 to 255 characters." },
		{ Column(new ExcelColumnOptions { Width = 255.01 }), "has the width 255.01" },
		{ Column(new ExcelColumnOptions { Width = double.NaN }), "has the width NaN" },
		{ Column(new ExcelColumnOptions { Format = "" }), "The column \"Amount\" has an empty format: use null for none." },
		{ Column(new ExcelColumnOptions { Format = "0.00 kg" }), "The column \"Amount\" has a format Excel doesn't open. The format code \"0.00 kg\" has the letter k outside quotes" },
		{ Column(null!), "Columns has no options for the column \"Amount\": leave it out for none." },
	};

	[Theory]
	[MemberData(nameof(Refused))]
	public void RefusesOptionsExcelWouldntOpen(ExcelSheetOptions options, string message)
	{
		var given = Assert.Throws<ArgumentException>(() => SheetSettings.Resolve(options, null, null, ExcelWriterOptions.Default));
		var workbook = Assert.Throws<ArgumentException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Sheets = options }));

		Assert.StartsWith("ExcelSheetOptions: ", given.Message);
		Assert.Contains(message, given.Message);
		Assert.Equal("options", given.ParamName);
		Assert.StartsWith("ExcelWriterOptions.Sheets: ", workbook.Message);
		Assert.Contains(message, workbook.Message);
	}

	[Fact]
	public void TakesOptionsAtTheirLimits()
	{
		var options = new ExcelSheetOptions
		{
			Title = new string('t', 32767),
			FreezeColumns = 16383,
			AutoFit = 1048575,
			MaxAutoFitWidth = 255,
			Table = new ExcelTableOptions { Name = new string('T', 255), Style = "TABLESTYLEDARK11" },
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["a"] = new() { Width = 0 },
				["b"] = new() { Width = 255, Format = "[DBNum3]0" },
				[""] = new() { Format = "General" },
			},
		};

		var settings = Settings(options);

		Assert.Equal(1048575, settings.AutoFit);
		Assert.Equal(1, Settings(new ExcelSheetOptions { MaxAutoFitWidth = 1 }).MaxAutoFitWidth);
		Assert.Null(Settings(new ExcelSheetOptions { Table = new ExcelTableOptions { Style = null } }).Table!.Style);
	}

	[Theory]
	[InlineData("TableStyleLight1")]
	[InlineData("TableStyleLight21")]
	[InlineData("TableStyleMedium1")]
	[InlineData("TableStyleMedium28")]
	[InlineData("TableStyleDark1")]
	[InlineData("TableStyleDark11")]
	[InlineData("tablestylemedium2")]
	public void TakesExcelsBuiltInTableStylesInAnyCase(string style) => Assert.True(ExcelTableOptions.IsBuiltInStyle(style));

	[Theory]
	[InlineData("TableStyleLight22")]
	[InlineData("TableStyleMedium0")]
	[InlineData("TableStyleDark12")]
	[InlineData("TableStyleMedium")]
	[InlineData("TableStyleMedium+2")]
	[InlineData("TableStyleMedium 2")]
	[InlineData("PivotStyleLight1")]
	[InlineData("TableStyleMedium99999999999")]
	public void RefusesOtherTableStyles(string style) => Assert.False(ExcelTableOptions.IsBuiltInStyle(style));

	[Fact]
	public void RefusesTheRecordTypesAttributesWhenTheyArentValid()
	{
		Assert.Equal("The [ExcelSheet] of BadSheet: FreezeColumns is -1, and is 0 to 16,383.", AttributeProblem<BadSheet>());
		Assert.Equal("The [ExcelSheet] of TableNameWithoutTable: TableName is \"Orders\", and Table is off: set Table = true for a table.", AttributeProblem<TableNameWithoutTable>());
		Assert.StartsWith("The [ExcelSheet] of BadTableName: The table has the name \"A1\", which is a cell reference", AttributeProblem<BadTableName>());
		Assert.Equal("The [ExcelColumn] of BadColumnWidth.Count has the width 300, and Excel's widths are 0 to 255 characters.", AttributeProblem<BadColumnWidth>());
		Assert.StartsWith("The [ExcelColumn] of BadColumnFormat.Count has a format Excel doesn't open. The format code \"0.00 kg\"", AttributeProblem<BadColumnFormat>());
		Assert.Equal(
			"The [ExcelColumn] of ExcelColumnOnNested.Address is on a member that nests a record, whose members have their own columns: put it on those members.",
			AttributeProblem<ExcelColumnOnNested>());
	}

	[Fact]
	public void TakesAColumnsOptionsByNameOverItsAttributeWhole()
	{
		var options = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { Wrap = true }, ["Name"] = new() { Width = 30 } },
		};
		var settings = SheetSettings.Resolve(options, typeof(SheetedOrder), MemberFormats.For<SheetedOrder>(), ExcelWriterOptions.Default);

		var columns = settings.ColumnsFor(["Amount", "Note", "Name"])!;

		Assert.Equal(new ExcelColumnOptions { Width = 12, Format = "#,##0.00" }, columns[0]);
		// The attribute's Hidden isn't merged in.
		Assert.Equal(new ExcelColumnOptions { Wrap = true }, columns[1]);
		Assert.Equal(new ExcelColumnOptions { Width = 30 }, columns[2]);
	}

	[Fact]
	public void TakesTheAttributesOfAColumnWithoutOptions()
	{
		var settings = SheetSettings.Resolve(null, typeof(SheetedOrder), MemberFormats.For<SheetedOrder>(), ExcelWriterOptions.Default);

		// The header's names say where the members are, ordinally, as for formats.
		var columns = settings.ColumnsFor(["Name", "note", "Note", "Amount"])!;

		Assert.Equal<IEnumerable<ExcelColumnOptions?>>([null, null, new ExcelColumnOptions { Hidden = true }, new ExcelColumnOptions { Width = 12, Format = "#,##0.00" }], columns);
	}

	[Fact]
	public void GivesEveryColumnOfANameItsOptions()
	{
		var settings = Settings(new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["When"] = new() { Width = 10 } } });

		var columns = settings.ColumnsFor(["When", "x", "When"])!;

		Assert.Equal<IEnumerable<ExcelColumnOptions?>>([new ExcelColumnOptions { Width = 10 }, null, new ExcelColumnOptions { Width = 10 }], columns);
	}

	[Fact]
	public void RefusesColumnsTheHeaderDoesntHave()
	{
		var settings = Settings(new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { Width = 10 }, ["name"] = new(), ["Totla"] = new() },
		});
		var withoutHeader = SheetSettings.Resolve(
			new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() } }, null, null,
			new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } });

		var error = Assert.Throws<InvalidOperationException>(() => settings.ColumnsFor(["Name", "Total"]));
		var none = Assert.Throws<InvalidOperationException>(() => withoutHeader.ColumnsFor(null));

		// In ordinal order.
		Assert.Equal("ExcelSheetOptions.Columns names columns that the header doesn't have: \"Totla\", \"name\". Names match the header's ordinally.", error.Message);
		Assert.Equal("ExcelSheetOptions.Columns names columns, and the sheet has no header to find them by (CsvWriterOptions.HasHeader is off): \"Name\".", none.Message);
		Assert.Equal<IEnumerable<ExcelColumnOptions?>>([new ExcelColumnOptions()], withoutHeader.ColumnsFor(["Name"])!);
	}

	[Fact]
	public void GivesMembersColumnsByTheirIndexesAndConstructorParameters()
	{
		var names = HeaderCapture.Of<ColumnedPosition>()!;
		var settings = SheetSettings.Resolve(null, typeof(ColumnedPosition), MemberFormats.For<ColumnedPosition>(), ExcelWriterOptions.Default);

		var columns = settings.ColumnsFor(names)!;

		Assert.Equal(["Name", "Note", "Day", "Day", "Converted"], names);
		Assert.Equal(new ExcelColumnOptions { Width = 20 }, columns[0]);
		Assert.Equal(new ExcelColumnOptions { Wrap = true }, columns[1]);
		Assert.Equal(new ExcelColumnOptions { Format = "0.0" }, columns[2]);
		Assert.Equal(new ExcelColumnOptions { Format = "0.0" }, columns[3]);
		Assert.Equal(new ExcelColumnOptions { Width = 25 }, columns[4]);
		// A converter's member has Excel options but no format: the sink reads its cells as any other.
		Assert.False(MemberFormats.For<ColumnedPosition>()!.HasFormats);
	}

	[Fact]
	public void CopiesTheColumnsWhenTheSheetIsAdded()
	{
		var columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { Width = 10 } };
		var settings = Settings(new ExcelSheetOptions { Columns = columns });

		columns["Name"] = new ExcelColumnOptions { Width = 300 };
		columns["Other"] = new ExcelColumnOptions();

		Assert.Equal<IEnumerable<ExcelColumnOptions?>>([new ExcelColumnOptions { Width = 10 }], settings.ColumnsFor(["Name"])!);
	}

	[Fact]
	public void ChecksTheWorkbooksColumnsAgainWhenTheyChange()
	{
		var columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { Width = 10 } };
		var options = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { Columns = columns, Title = "Report" } };
		using var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);

		columns["Name"] = new ExcelColumnOptions { Format = "0 kg" };

		var error = Assert.Throws<InvalidOperationException>(() => book.AddSheet("Data"));
		Assert.StartsWith("ExcelWriterOptions.Sheets' columns were changed after the writer was created: The column \"Name\" has a format Excel doesn't open.", error.Message);
		// The empty sheet of a workbook without sheets shows nothing, whatever the workbook's options say.
		book.Dispose();
		Assert.Equal(["Sheet1: "], Written.Sheets(stream.ToArray()));
		Assert.Empty(Written.ValidationErrors(stream.ToArray()));
	}

	[Fact]
	public void GivesAnEmptyWorkbooksSheetNothingToShow()
	{
		var empty = SheetSettings.Empty;

		Assert.Null(empty.Title);
		Assert.False(empty.BoldHeader);
		Assert.False(empty.FreezeHeader);
		Assert.False(empty.AutoFilter);
		Assert.Null(empty.Table);
		Assert.Equal(0, empty.AutoFit);
		Assert.Null(empty.ColumnsFor(null));
	}

	[Fact]
	public void RefusesOtherWorkbookOptions()
	{
		var sheets = Assert.Throws<ArgumentException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Sheets = null! }));
		var links = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Links = (ExcelLinks)3 }));

		Assert.StartsWith("ExcelWriterOptions.Sheets can't be null: use ExcelSheetOptions.Default.", sheets.Message);
		Assert.Equal("options", sheets.ParamName);
		Assert.StartsWith("ExcelWriterOptions.Links is not an ExcelLinks value.", links.Message);
		Assert.Equal("options", links.ParamName);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AddsSheetsWithTheirSettings(bool async)
	{
		using var stream = new MemoryStream();
		var options = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFit = 7 } };
		var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
		var seen = new List<ExcelSheetOptions>();

		using (book.AddSheet("Given", Given))
		{
			seen.Add(book.OpenSheet!.Settings.Sheet);
		}

		using (book.AddSheet("Workbook's"))
		{
			seen.Add(book.OpenSheet!.Settings.Sheet);
		}

		using (book.AddSheet<SheetedOrder>("Attribute's"))
		{
			seen.Add(book.OpenSheet!.Settings.Sheet);
		}

		using (book.AddSheet<SheetedOrder>("Given over the attribute", Given))
		{
			seen.Add(book.OpenSheet!.Settings.Sheet);
		}

		// WriteSheet and its async forms take options, and the record type's attribute without them.
		var order = new SheetedOrder { Amount = 1.5m, Note = "n", Name = "a" };
		if (async)
		{
			await book.WriteSheetAsync("Async", [order], Given, CancellationToken.None);
			await book.WriteSheetAsync("Async attribute's", Sequence(order));
		}
		else
		{
			book.WriteSheet("Sync", [order], Given);
			book.WriteSheet("Sync attribute's", [order]);
		}

		Assert.Same(Given, seen[0]);
		Assert.Same(options.Sheets, seen[1]);
		Assert.Equal("Orders", seen[2].Title);
		Assert.Same(Given, seen[3]);

		await book.DisposeAsync();
		var workbook = stream.ToArray();
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(6, Written.Read(workbook).Count);
	}

	[Fact]
	public void AddsNoSheetWhenItsSettingsThrow()
	{
		using var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true);

		Assert.Throws<ArgumentException>(() => book.AddSheet("Data", new ExcelSheetOptions { FreezeColumns = -1 }));
		Assert.Throws<InvalidOperationException>(() => book.AddSheet<BadSheet>("Data"));
		Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Data", new[] { new BadColumnWidth() }));
		Assert.Empty(book.SheetNames);
		// The name is still free, and the workbook can be completed.
		using (var writer = book.AddSheet("Data"))
		{
			writer.WriteField("a");
			writer.EndRecord();
		}

		book.Dispose();
		Assert.Equal(["Data: a"], Written.Sheets(stream.ToArray()));
	}

	/// <summary>Options with one column's options, named <c>Amount</c>.</summary>
	/// <param name="column">The column's options.</param>
	/// <returns>The options.</returns>
	private static ExcelSheetOptions Column(ExcelColumnOptions column) =>
		new() { Columns = new Dictionary<string, ExcelColumnOptions> { ["Amount"] = column } };

	/// <summary>A sheet's settings from options given, with the default workbook options.</summary>
	/// <param name="options">The options.</param>
	/// <returns>The settings.</returns>
	private static SheetSettings Settings(ExcelSheetOptions options) => SheetSettings.Resolve(options, null, null, ExcelWriterOptions.Default);

	/// <summary>What adding a sheet of a record type throws for its Excel attributes.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <returns>The exception's message.</returns>
	private static string AttributeProblem<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(MemberFormats.Members)] T>()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());

		return Assert.Throws<InvalidOperationException>(() => book.AddSheet<T>("Data")).Message;
	}

	/// <summary>Records as an asynchronous sequence.</summary>
	/// <param name="records">The records.</param>
	/// <returns>The sequence.</returns>
	private static async IAsyncEnumerable<SheetedOrder> Sequence(params SheetedOrder[] records)
	{
		foreach (var record in records)
		{
			await Task.Yield();
			yield return record;
		}
	}
}
