using System.Diagnostics;
using System.IO.Compression;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Skips a theory unless <c>CSVHELPER_EXCEL_ZIP64</c> is <c>1</c>: a test that writes and reads gigabytes, which runs on
/// demand.
/// </summary>
public sealed class Zip64TheoryAttribute : TheoryAttribute
{
	/// <summary>The environment variable that runs the tests.</summary>
	public const string Variable = "CSVHELPER_EXCEL_ZIP64";

	/// <summary>Creates the attribute, skipping the theory unless the variable is <c>1</c>.</summary>
	public Zip64TheoryAttribute()
	{
		if (Environment.GetEnvironmentVariable(Variable) != "1")
		{
			Skip = $"It writes a sheet past 4 GiB, on demand: set {Variable} to 1.";
		}
	}
}

/// <summary>
/// A sheet whose XML is past 4 GiB, so that its entry needs Zip64 sizes, written through
/// <see cref="ExcelWorkbookWriter.ToFile"/> (a stream that seeks) and to a stream that can't seek, synchronously and
/// asynchronously: Phase 4's "Risks" (<see cref="ZipArchive"/>'s support differs between targets). The package is read
/// back with <see cref="ZipArchive"/>, whole, and with CsvHelper.Excel's reader. Each case writes about 4.6 GB of XML and
/// reads it twice, so the tests run on demand (<see cref="Zip64TheoryAttribute"/>).
/// </summary>
/// <param name="output">Shows the sizes and times of each case.</param>
public class Zip64Tests(ITestOutputHelper output)
{
	/// <summary>The rows written.</summary>
	private const int Rows = 1_000_000;

	/// <summary>The cells of a row.</summary>
	private const int Columns = 4;

	/// <summary>Each cell's characters: a row's four make over 4,295 bytes of XML, so that a million rows pass 4 GiB.</summary>
	private const int CellLength = 1_100;

	/// <summary>The digits of a cell's number, its row's and its column's, at its start.</summary>
	private const int NumberLength = 8;

	/// <summary>The sheet's name.</summary>
	private const string SheetName = "Big";

	/// <summary>
	/// Writes a million rows of four 1,100-character cells, then checks that the sheet's entry is past 4 GiB, that it
	/// inflates to its stated length, and that CsvHelper.Excel reads every row back.
	/// </summary>
	/// <param name="seekable">Whether the workbook is written through <see cref="ExcelWorkbookWriter.ToFile"/>, rather than to a stream that can't seek.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Zip64Theory]
	[InlineData(true, false)]
	[InlineData(true, true)]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public async Task WritesASheetPast4GiB(bool seekable, bool async)
	{
		var path = Path.Combine(Path.GetTempPath(), $"csvhelper-zip64-{Guid.NewGuid():N}.xlsx");
		var clock = Stopwatch.StartNew();
		try
		{
			var workbook = seekable
				? ExcelWorkbookWriter.ToFile(path)
				: ExcelWorkbookWriter.Create(new ForwardOnlyStream(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, async)));
			if (async)
			{
				await using (workbook)
				{
					await WriteRowsAsync(workbook, true);
				}
			}
			else
			{
				using (workbook)
				{
					await WriteRowsAsync(workbook, false);
				}
			}

			var written = clock.Elapsed;
			using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
			{
				var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!;
				Assert.True(sheet.Length > uint.MaxValue, $"The sheet's XML is {sheet.Length:N0} bytes, not past 4 GiB.");
				Assert.Equal(sheet.Length, Inflate(sheet));
				output.WriteLine($"The sheet's XML is {sheet.Length:N0} bytes, deflated to {sheet.CompressedLength:N0}, in a file of {new FileInfo(path).Length:N0}; written in {written.TotalSeconds:F1} s.");
			}

			clock.Restart();

			var options = new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false }, MaxUncompressedSize = 8L << 30 };
			using var reader = ExcelReader.FromFile(path, options);
			var expected = new char[CellLength];
			var rows = 0;
			while (reader.Read())
			{
				Assert.Equal(Columns, reader.FieldCount);

				// Every row is read; some are compared whole.
				if (rows % 4096 == 0 || rows == Rows - 1)
				{
					for (var column = 0; column < Columns; column++)
					{
						Assert.Equal(new string(Cell(expected, rows, column)), reader.GetString(column));
					}
				}

				rows++;
			}

			Assert.Equal(Rows, rows);
			output.WriteLine($"CsvHelper.Excel read its {rows:N0} rows in {clock.Elapsed.TotalSeconds:F1} s.");
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>Writes the rows to a sheet, field by field.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="async">Whether to end records, and the sheet, asynchronously.</param>
	/// <returns>A task that completes when the sheet is complete.</returns>
	private static async Task WriteRowsAsync(ExcelWorkbookWriter workbook, bool async)
	{
		var cell = new char[CellLength];
		var writer = workbook.AddSheet(SheetName);
		try
		{
			for (var row = 0; row < Rows; row++)
			{
				for (var column = 0; column < Columns; column++)
				{
					writer.WriteField(new ReadOnlySpan<char>(Cell(cell, row, column)));
				}

				if (async)
				{
					await writer.EndRecordAsync();
				}
				else
				{
					writer.EndRecord();
				}
			}
		}
		finally
		{
			if (async)
			{
				await writer.DisposeAsync();
			}
			else
			{
				writer.Dispose();
			}
		}
	}

	/// <summary>Fills a cell's characters: its row's seven digits and its column's one, then letters.</summary>
	/// <param name="cell">The characters, <see cref="CellLength"/> of them.</param>
	/// <param name="row">The row, from 0.</param>
	/// <param name="column">The column, from 0.</param>
	/// <returns>The characters.</returns>
	private static char[] Cell(char[] cell, int row, int column)
	{
		var number = row;
		for (var i = NumberLength - 2; i >= 0; i--)
		{
			cell[i] = (char)('0' + (number % 10));
			number /= 10;
		}

		cell[NumberLength - 1] = (char)('0' + column);
		for (var i = NumberLength; i < cell.Length; i++)
		{
			cell[i] = (char)('a' + ((i + row) % 26));
		}

		return cell;
	}

	/// <summary>Inflates an entry whole.</summary>
	/// <param name="entry">The entry.</param>
	/// <returns>The bytes it inflated to.</returns>
	private static long Inflate(ZipArchiveEntry entry)
	{
		using var stream = entry.Open();
		var buffer = new byte[1 << 20];
		var total = 0L;
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			total += read;
		}

		return total;
	}

	/// <summary>A stream that writes forward only: it can't seek or tell its position. It disposes the stream it writes to.</summary>
	/// <param name="inner">The stream written to.</param>
	private sealed class ForwardOnlyStream(Stream inner) : Stream
	{
		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => true;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush() => inner.Flush();

		/// <inheritdoc/>
		public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

		/// <inheritdoc/>
		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				inner.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
