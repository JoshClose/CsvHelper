using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// An import template of <see cref="OrderRecord"/>, written by <see cref="ExcelTemplate.Create{T}(Stream, ExcelTemplateOptions?)"/>
/// to a stream that counts and drops its bytes: the header of its 12 members, each column validated by its member's type,
/// and the sheet protected.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CsvHelper_WriteSheet"/>, the baseline, writes the same sheet without records, validations or protection,
/// through <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>.
/// <see cref="CsvHelper_Create"/> writes the template; <see cref="CsvHelper_CreatePassword"/> protects it with a password,
/// hashed with SHA-512 and 100,000 spins, as Excel hashes one.
/// </para>
/// <para>
/// The Written column is the size of each workbook.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelTemplateCreate : ExcelWriteBenchmarkBase
{
	/// <summary>The template's options with a password.</summary>
	private static readonly ExcelTemplateOptions WithPassword = new() { Sheet = new ExcelSheetOptions { Protect = true, Password = "secret" } };

	/// <summary>CsvHelper.Excel: a sheet of the orders' header without records, at the defaults.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Template")]
	public long CsvHelper_WriteSheet()
	{
		var stream = NewOutput();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			workbook.WriteSheet<OrderRecord>(nameof(OrderRecord), []);
		}

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: <see cref="ExcelTemplate.Create{T}(Stream, ExcelTemplateOptions?)"/>, without a password.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Template")]
	public long CsvHelper_Create()
	{
		var stream = NewOutput();
		ExcelTemplate.Create<OrderRecord>(stream);

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: <see cref="ExcelTemplate.Create{T}(Stream, ExcelTemplateOptions?)"/>, with a password.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Template")]
	public long CsvHelper_CreatePassword()
	{
		var stream = NewOutput();
		ExcelTemplate.Create<OrderRecord>(stream, WithPassword);

		return stream.Count;
	}

	/// <summary>Reads what the last benchmark wrote, which it kept, back as orders: none, under the header.</summary>
	/// <returns>The orders.</returns>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<OrderRecord>().ToList();
		}
	}
}
