using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;
using MiniExcelLibs;
using EdrFactory = ExcelDataReader.ExcelReaderFactory;
using EdrReader = ExcelDataReader.IExcelDataReader;
using OpenXmlReader = DocumentFormat.OpenXml.OpenXmlReader;
using SdkCell = DocumentFormat.OpenXml.Spreadsheet.Cell;
using SdkCellValue = DocumentFormat.OpenXml.Spreadsheet.CellValue;
using SdkRow = DocumentFormat.OpenXml.Spreadsheet.Row;
using SdkSheet = DocumentFormat.OpenXml.Spreadsheet.Sheet;
using SdkText = DocumentFormat.OpenXml.Spreadsheet.Text;
using SharedStringItem = DocumentFormat.OpenXml.Spreadsheet.SharedStringItem;
using SpreadsheetDocument = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument;
using SylvanExcelReader = Sylvan.Data.Excel.ExcelDataReader;
using SylvanWorkbookType = Sylvan.Data.Excel.ExcelWorkbookType;
using WorksheetPart = DocumentFormat.OpenXml.Packaging.WorksheetPart;

namespace CsvHelper.Benchmarks;

/// <summary>Column strings from ExcelDataReader, which gives an empty cell as <see langword="null"/>.</summary>
/// <param name="reader">The reader, on a row.</param>
internal readonly struct ExcelDataReaderSource(EdrReader reader) : IColumnSource
{
	/// <inheritdoc/>
	public string Get(int index) => reader.GetString(index) ?? "";
}

/// <summary>Column strings from Sylvan.Data.Excel.</summary>
/// <param name="reader">The reader, on a row.</param>
internal readonly struct SylvanExcelSource(SylvanExcelReader reader) : IColumnSource
{
	/// <inheritdoc/>
	public string Get(int index) => reader.GetString(index);
}

/// <summary>Column strings from a row dictionary keyed by header name, as MiniExcel gives a row.</summary>
/// <param name="row">The row, whose value for an empty cell is <see langword="null"/>.</param>
/// <param name="names">The header names, by column.</param>
internal readonly struct DictionarySource(IDictionary<string, object?> row, string[] names) : IColumnSource
{
	/// <inheritdoc/>
	public string Get(int index) => row[names[index]] as string ?? "";
}

/// <summary>Column strings from an array of a row's values.</summary>
/// <param name="values">The values, by column.</param>
internal readonly struct ArraySource(string[] values) : IColumnSource
{
	/// <inheritdoc/>
	public string Get(int index) => values[index];
}

/// <summary>
/// NCsvPerf's package assets data as a workbook: a header row and a million rows of 25 short text columns, read by each
/// library from the same bytes. Rows = find the rows; Strings = every field of every row as a string; Records = a
/// <see cref="PackageAsset"/> per row. <see cref="Strings"/> chooses between shared strings, as Excel writes them, and
/// inline strings.
/// </summary>
/// <remarks>
/// The MB/s column is of the same data as CSV, so it compares with the CSV benchmarks', and the <c>Csv_</c> methods read
/// that CSV with CsvHelper, as a reference. See <see cref="ExcelDataGenerator"/> for the data, and the Phase 1 plan's
/// "Baseline benchmark" for how each library reads it.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelPackageAssetsRead : ReadBenchmarkBase
{
	/// <summary>The workbook every library reads, for the current <see cref="Strings"/>.</summary>
	private byte[] workbook = [];

	/// <summary>Gets or sets where the cells' strings are.</summary>
	[Params(ExcelStrings.Shared, ExcelStrings.Inline)]
	public ExcelStrings Strings { get; set; }

	/// <inheritdoc/>
	protected override string Generate() => ExcelDataGenerator.PackageAssetsCsv(ExcelDataGenerator.PackageAssetRows);

	/// <inheritdoc/>
	protected override void AfterSetup()
	{
		// ExcelDataReader needs the code pages on .NET, which has only the UTF encodings built in.
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		workbook = ExcelDataGenerator.PackageAssets(ExcelDataGenerator.PackageAssetRows, Strings);

		// The CSV's bytes are kept for the reference benchmarks and the MB/s column; its string isn't needed.
		Text = "";
	}

	// ----- Rows -----

	/// <summary>Sylvan.Data.Excel: counts the rows. It reads the header row when it opens.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Sylvan_Rows()
	{
		using var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml);
		var n = 0;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	/// <summary>CsvHelper.Excel: counts the records after the header.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());
		var n = 0;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	/// <summary>ExcelDataReader: counts the rows after the header row, which it returns as the first.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int ExcelDataReader_Rows()
	{
		using var reader = EdrFactory.CreateOpenXmlReader(NewWorkbookStream());
		var n = -1;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	/// <summary>MiniExcel: counts the rows its query returns, the header row giving the keys.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int MiniExcel_Rows()
	{
		var n = 0;
		foreach (var _ in MiniExcelRows())
		{
			n++;
		}

		return n;
	}

	/// <summary>The Open XML SDK's SAX reader: counts the sheet's row elements after the header's.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int OpenXml_Rows()
	{
		using var document = SpreadsheetDocument.Open(NewWorkbookStream(), false);
		using var reader = OpenXmlReader.Create(FirstWorksheet(document));
		var n = -1;
		while (reader.Read())
		{
			if (reader.IsStartElement && reader.ElementType == typeof(SdkRow))
			{
				n++;
			}
		}

		return n;
	}

	/// <summary>CsvHelper reading the same data as CSV, for reference.</summary>
	/// <returns>The number of data rows.</returns>
	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Csv_Rows()
	{
		using var reader = CsvReader.Create(NewStream());
		var n = 0;
		while (reader.Read())
		{
			n++;
		}

		return n;
	}

	// ----- Strings -----

	/// <summary>Sylvan.Data.Excel: every field as a string.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Strings")]
	public long Sylvan_Strings()
	{
		using var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	/// <summary>CsvHelper.Excel: every field as a string.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long CsvHelper_Strings()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	/// <summary>ExcelDataReader: every field as a string, after the header row.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long ExcelDataReader_Strings()
	{
		using var reader = EdrFactory.CreateOpenXmlReader(NewWorkbookStream());
		reader.Read();

		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i)?.Length ?? 0;
			}
		}

		return total;
	}

	/// <summary>MiniExcel: every value of every row dictionary.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long MiniExcel_Strings()
	{
		var total = 0L;
		foreach (var row in MiniExcelRows())
		{
			foreach (var value in row.Values)
			{
				total += (value as string)?.Length ?? 0;
			}
		}

		return total;
	}

	/// <summary>The Open XML SDK's SAX reader: every cell's value, with the shared strings read first.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long OpenXml_Strings()
	{
		var total = 0L;
		foreach (var values in OpenXmlRows())
		{
			for (var i = 0; i < values.Length; i++)
			{
				total += values[i].Length;
			}
		}

		return total;
	}

	/// <summary>CsvHelper reading the same data as CSV, for reference.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	[BenchmarkCategory("Strings")]
	public long Csv_Strings()
	{
		using var reader = CsvReader.Create(NewStream());
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	// ----- Records -----

	/// <summary>Sylvan.Data.Excel: a record per row, from its strings.</summary>
	/// <returns>The records.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sylvan_Records()
	{
		using var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml);
		var list = new List<PackageAsset>();
		var source = new SylvanExcelSource(reader);
		while (reader.Read())
		{
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	/// <summary>CsvHelper.Excel: the records, through the mapper generated for <see cref="PackageAsset"/>.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.GetRecords<PackageAsset>().ToList();
	}

	/// <summary>ExcelDataReader: a record per row after the header row, from its strings.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> ExcelDataReader_Records()
	{
		using var reader = EdrFactory.CreateOpenXmlReader(NewWorkbookStream());
		reader.Read();

		var list = new List<PackageAsset>();
		var source = new ExcelDataReaderSource(reader);
		while (reader.Read())
		{
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	/// <summary>MiniExcel: a record per row dictionary, its values found by header name.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> MiniExcel_Records()
	{
		var list = new List<PackageAsset>();
		foreach (var row in MiniExcelRows())
		{
			var source = new DictionarySource(row, DataGenerator.PackageAssetHeader);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	/// <summary>The Open XML SDK's SAX reader: a record per row, from its cells' values.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> OpenXml_Records()
	{
		var list = new List<PackageAsset>();
		foreach (var values in OpenXmlRows())
		{
			var source = new ArraySource(values);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	/// <summary>CsvHelper reading the same data as CSV, for reference.</summary>
	/// <returns>The records.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Csv_Records()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<PackageAsset>().ToList();
	}

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewWorkbookStream() => new(workbook, writable: false);

	/// <summary>MiniExcel's rows after the header row, as dictionaries keyed by header name.</summary>
	/// <returns>The rows.</returns>
	private IEnumerable<IDictionary<string, object?>> MiniExcelRows()
	{
		// Its rows are typed dynamic; taking them as objects keeps the runtime binder out of the loop.
		IEnumerable<object> rows = MiniExcel.Query(NewWorkbookStream(), useHeaderRow: true, excelType: ExcelType.XLSX);
		foreach (var row in rows)
		{
			yield return (IDictionary<string, object?>)row;
		}
	}

	/// <summary>
	/// The Open XML SDK's SAX reader: each row after the header row as its values by column, an empty cell as an empty
	/// string. The shared strings are read first, into an array.
	/// </summary>
	/// <returns>The rows' values.</returns>
	private IEnumerable<string[]> OpenXmlRows()
	{
		using var document = SpreadsheetDocument.Open(NewWorkbookStream(), false);

		var shared = new List<string>();
		var sharedPart = document.WorkbookPart!.SharedStringTablePart;
		if (sharedPart != null)
		{
			using var strings = OpenXmlReader.Create(sharedPart);
			while (strings.Read())
			{
				if (strings.IsStartElement && strings.ElementType == typeof(SharedStringItem))
				{
					shared.Add(((SharedStringItem)strings.LoadCurrentElement()!).InnerText);
				}
			}
		}

		var table = shared.ToArray();
		var columns = DataGenerator.PackageAssetHeader.Length;

		using var reader = OpenXmlReader.Create(FirstWorksheet(document));
		var header = true;
		string[]? values = null;
		var column = 0;
		var isShared = false;
		while (reader.Read())
		{
			var type = reader.ElementType;
			if (type == typeof(SdkRow))
			{
				if (reader.IsStartElement)
				{
					values = new string[columns];
					Array.Fill(values, "");
				}
				else if (header)
				{
					header = false;
				}
				else
				{
					yield return values!;
				}
			}
			else if (type == typeof(SdkCell) && reader.IsStartElement)
			{
				isShared = false;
				foreach (var attribute in reader.Attributes)
				{
					if (attribute.LocalName == "r")
					{
						column = ColumnIndex(attribute.Value!);
					}
					else if (attribute.LocalName == "t")
					{
						isShared = attribute.Value == "s";
					}
				}
			}
			else if ((type == typeof(SdkCellValue) || type == typeof(SdkText)) && reader.IsStartElement)
			{
				var text = reader.GetText();
				values![column] = isShared ? table[int.Parse(text, CultureInfo.InvariantCulture)] : text;
			}
		}
	}

	/// <summary>The first sheet's part, which is the workbook's only worksheet.</summary>
	/// <param name="document">The workbook.</param>
	/// <returns>The part.</returns>
	private static WorksheetPart FirstWorksheet(SpreadsheetDocument document)
	{
		var workbookPart = document.WorkbookPart!;
		var sheet = workbookPart.Workbook!.Sheets!.Elements<SdkSheet>().First();

		return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
	}

	/// <summary>The 0-based column of a cell reference such as <c>Y12</c>.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The column.</returns>
	private static int ColumnIndex(string reference)
	{
		var column = 0;
		for (var i = 0; i < reference.Length && reference[i] >= 'A'; i++)
		{
			column = column * 26 + reference[i] - 'A' + 1;
		}

		return column - 1;
	}
}
