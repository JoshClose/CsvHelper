namespace CsvHelper.Excel.Tests.Api;

/// <summary><see cref="ExcelReader"/>: one call for a reader that owns its workbook. Each test runs synchronously and asynchronously.</summary>
public class ExcelReaderTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsAFileAndClosesItWithTheReader(bool async)
	{
		var path = Workbooks.ToFile(Workbooks.Orders());
		try
		{
			var options = new ExcelReaderOptions { Sheet = "Orders" };
			var reader = async ? await ExcelReader.FromFileAsync(path, options) : ExcelReader.FromFile(path, options);
			Assert.Equal(Workbooks.OrderRows, await reader.ReadAllAsync(async));
			Assert.False(Workbooks.IsClosed(path));

			if (async)
			{
				await reader.DisposeAsync();
			}
			else
			{
				reader.Dispose();
			}

			Assert.True(Workbooks.IsClosed(path));

			// A reader that can't be opened doesn't hold the file either.
			var e = await Assert.ThrowsAsync<ArgumentException>(async () => _ = async ? await ExcelReader.FromFileAsync(path, new ExcelReaderOptions { Sheet = "Nope" }) : ExcelReader.FromFile(path, new ExcelReaderOptions { Sheet = "Nope" }));
			Assert.Equal("options", e.ParamName);
			Assert.True(Workbooks.IsClosed(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsAStreamAndDisposesItUnlessLeftOpen(bool async)
	{
		foreach (var leaveOpen in new[] { false, true })
		{
			var stream = Workbooks.Orders().ToStream();
			var reader = async ? await ExcelReader.CreateAsync(stream, leaveOpen: leaveOpen) : ExcelReader.Create(stream, leaveOpen: leaveOpen);

			// The first worksheet, hidden ones included.
			Assert.Equal(["hidden"], reader.Headers);
			reader.Dispose();
			Assert.Equal(leaveOpen, stream.CanRead);
		}

		// A reader that can't be opened leaves the caller's stream open.
		var unopened = Workbooks.Orders().ToStream();
		await Assert.ThrowsAsync<NotSupportedException>(async () => _ = async ? await ExcelReader.CreateAsync(unopened, new ExcelReaderOptions { SheetIndex = 0 }) : ExcelReader.Create(unopened, new ExcelReaderOptions { SheetIndex = 0 }));
		Assert.True(unopened.CanRead);
	}
}
