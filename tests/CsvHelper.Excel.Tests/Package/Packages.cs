using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>Opens <see cref="TestPackage"/>s with the package layer, synchronously or asynchronously.</summary>
internal static class Packages
{
	/// <summary>The default limits: <c>MaxPartSize</c> 256 MiB and <c>MaxUncompressedSize</c> 4 GiB.</summary>
	public static PartLimits Limits { get; } = new(256L * 1024 * 1024, 4L * 1024 * 1024 * 1024);

	/// <summary>
	/// Runs code written for both modes: asynchronously, or synchronously, where its task must have completed when it
	/// returns (<see cref="SyncOrAsync"/> throws otherwise).
	/// </summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	/// <param name="async">Whether to run asynchronously.</param>
	/// <param name="run">The code, given the mode.</param>
	/// <returns>The result.</returns>
	public static async Task<T> Run<T>(bool async, Func<bool, ValueTask<T>> run) => async ? await run(true) : SyncOrAsync.Result(run(false));

	/// <summary>Opens a package's archive.</summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for <see cref="Limits"/>.</param>
	/// <returns>The archive.</returns>
	public static Task<PackageArchive> OpenAsync(this TestPackage package, bool async, PartLimits? limits = null) =>
		Run(async, a => PackageArchive.OpenAsync(package.ToStream(), false, limits ?? Limits, a, CancellationToken.None));

	/// <summary>Loads a package's workbook part.</summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to load it asynchronously.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for <see cref="Limits"/>.</param>
	/// <returns>The workbook part.</returns>
	public static async Task<WorkbookPart> LoadWorkbookAsync(this TestPackage package, bool async, PartLimits? limits = null)
	{
		using var archive = await package.OpenAsync(async, limits);

		return await Run(async, a => WorkbookPart.LoadAsync(archive, limits ?? Limits, a, CancellationToken.None));
	}

	/// <summary>Loads a package's styles, from the styles part its workbook names.</summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to load them asynchronously.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for <see cref="Limits"/>.</param>
	/// <returns>The styles.</returns>
	public static async Task<StyleTable> LoadStylesAsync(this TestPackage package, bool async, PartLimits? limits = null)
	{
		using var archive = await package.OpenAsync(async, limits);
		var workbook = await Run(async, a => WorkbookPart.LoadAsync(archive, limits ?? Limits, a, CancellationToken.None));

		return await Run(async, a => StyleTable.LoadAsync(archive, workbook.StylesPart, limits ?? Limits, a, CancellationToken.None));
	}

	/// <summary>
	/// Loads a package's shared strings, from the shared strings part its workbook names. The package opens with
	/// <see cref="Limits"/>, as a workbook's open does with its own limits, and its reads count toward the workbook's total.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to load them asynchronously.</param>
	/// <param name="limits">The limits of the shared strings' load, or <see langword="null"/> for <see cref="Limits"/>.</param>
	/// <returns>The shared strings.</returns>
	public static async Task<SharedStrings> LoadSharedStringsAsync(this TestPackage package, bool async, PartLimits? limits = null)
	{
		using var archive = await package.OpenAsync(async);
		var workbook = await Run(async, a => WorkbookPart.LoadAsync(archive, Limits, a, CancellationToken.None));

		return await Run(async, a => SharedStrings.LoadAsync(archive, workbook.SharedStringsPart, limits ?? Limits, a, CancellationToken.None));
	}

	/// <summary>The text of every string in a table, in order.</summary>
	/// <param name="table">The shared strings.</param>
	/// <returns>The strings.</returns>
	public static List<string> Strings(SharedStrings table)
	{
		var strings = new List<string>();
		for (var i = 0; i < table.Count; i++)
		{
			Assert.True(table.TryGet(i, out var text));
			strings.Add(Encoding.UTF8.GetString(text.ToArray()));
		}

		return strings;
	}

	/// <summary>Describes a sheet for comparisons: <c>index name visibility kind part</c>, or the problem in parentheses.</summary>
	/// <param name="sheet">The sheet.</param>
	/// <returns>The description.</returns>
	public static string Describe(WorkbookSheet sheet) =>
		$"{sheet.Index} {sheet.Name} {sheet.Visibility} {sheet.Kind} {sheet.PartName ?? "(" + sheet.Problem + ")"}";
}
