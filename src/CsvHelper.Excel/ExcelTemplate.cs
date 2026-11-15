using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Writes import templates: a workbook of one sheet of a record type's header, without records, for users to fill in. Each
/// column below the header takes what its member's type reads, and the header can't be changed.
/// </summary>
/// <remarks>
/// <para>
/// The sheet is <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>'s without
/// records, with <see cref="ExcelSheetOptions.Validate"/> and <see cref="ExcelSheetOptions.Protect"/> on, whatever look it
/// takes: <see cref="ExcelTemplateOptions.Sheet"/>, else the type's <see cref="ExcelSheetAttribute"/>, else
/// <see cref="ExcelWriterOptions.Sheets"/>, with its members' <see cref="ExcelColumnAttribute"/>s. So each column has its
/// member's type's validation and format: a dropdown of a <see cref="bool"/>'s or an enum's values, whole numbers or
/// numbers in the type's range, dates and times in their formats, and Text for text; and its
/// <see cref="ExcelColumnOptions.List"/> and <see cref="ExcelColumnOptions.Prompt"/>. The cells below the header in its
/// columns are unlocked, and the title, the header and the columns past it locked, with
/// <see cref="ExcelSheetOptions.Password"/> if it has one. Under auto-fit, a column is as wide as its header's name, a
/// date's, time's or duration's as its widest value, and Excel's default width at least.
/// </para>
/// <para>
/// The sheet is named after the type (<see cref="ExcelTemplateOptions.SheetName"/>). A sheet that needn't validate or be
/// protected, or that holds records to edit, is <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>'s
/// with the options asked for.
/// </para>
/// <para>
/// <b>Files and streams.</b> A path overload creates or overwrites its file, and deletes it when writing fails, so that
/// none is left half written. A stream overload writes from the stream's position and leaves it open. Every write of an
/// asynchronous overload is asynchronous, with the workbook writer's caveat before .NET 10 (<see cref="ExcelWorkbookWriter"/>).
/// </para>
/// </remarks>
public static class ExcelTemplate
{
	/// <summary>Writes a template of <typeparamref name="T"/> to a file.</summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="path">The path of the workbook, which is created or overwritten.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelTemplateOptions.Default"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The options are not valid; <see cref="ExcelTemplateOptions.SheetName"/> is not one Excel opens; the sheet's look has
	/// a table; or <see cref="ExcelTemplateOptions.Excel"/>'s <see cref="CsvWriterOptions.HasHeader"/> is off.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with; <typeparamref name="T"/> has no record
	/// writer, its Excel attributes are not valid, or its <see cref="ExcelSheetAttribute"/> has a table; or
	/// <see cref="ExcelSheetOptions.Columns"/> names a column that <typeparamref name="T"/>'s header doesn't have.
	/// </exception>
	/// <exception cref="IOException">The file can't be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The workbook can't be written with the caller's permissions.</exception>
	public static void Create<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string path, ExcelTemplateOptions? options = null) =>
		SyncOrAsync.Wait(CreateCoreAsync<T>(path, options, false, CancellationToken.None));

	/// <summary>Writes a template of <typeparamref name="T"/> to a stream, leaving it open. See <see cref="Create{T}(string, ExcelTemplateOptions?)"/>.</summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="stream">The stream the workbook is written to, from its position.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelTemplateOptions.Default"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The options are not valid; <see cref="ExcelTemplateOptions.SheetName"/> is not one Excel opens; the sheet's look has
	/// a table; <see cref="ExcelTemplateOptions.Excel"/>'s <see cref="CsvWriterOptions.HasHeader"/> is off; or
	/// <paramref name="stream"/> can't be written.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with; <typeparamref name="T"/> has no record
	/// writer, its Excel attributes are not valid, or its <see cref="ExcelSheetAttribute"/> has a table; or
	/// <see cref="ExcelSheetOptions.Columns"/> names a column that <typeparamref name="T"/>'s header doesn't have.
	/// </exception>
	/// <exception cref="IOException">The stream can't be written.</exception>
	public static void Create<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(Stream stream, ExcelTemplateOptions? options = null) =>
		SyncOrAsync.Wait(CreateCoreAsync<T>(stream, options, false, CancellationToken.None));

	/// <summary>Asynchronously writes a template of <typeparamref name="T"/> to a file. See <see cref="Create{T}(string, ExcelTemplateOptions?)"/>.</summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="path">The path of the workbook, which is created or overwritten.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelTemplateOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels writing, which then deletes the workbook.</param>
	/// <returns>A task that completes when the workbook is written and closed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The options are not valid; <see cref="ExcelTemplateOptions.SheetName"/> is not one Excel opens; the sheet's look has
	/// a table; or <see cref="ExcelTemplateOptions.Excel"/>'s <see cref="CsvWriterOptions.HasHeader"/> is off.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with; <typeparamref name="T"/> has no record
	/// writer, its Excel attributes are not valid, or its <see cref="ExcelSheetAttribute"/> has a table; or
	/// <see cref="ExcelSheetOptions.Columns"/> names a column that <typeparamref name="T"/>'s header doesn't have.
	/// </exception>
	/// <exception cref="IOException">The file can't be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The workbook can't be written with the caller's permissions.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask CreateAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string path, ExcelTemplateOptions? options = null, CancellationToken cancellationToken = default) =>
		CreateCoreAsync<T>(path, options, true, cancellationToken);

	/// <summary>
	/// Asynchronously writes a template of <typeparamref name="T"/> to a stream, leaving it open. See
	/// <see cref="Create{T}(string, ExcelTemplateOptions?)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="stream">The stream the workbook is written to, from its position.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelTemplateOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>A task that completes when the workbook is written.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The options are not valid; <see cref="ExcelTemplateOptions.SheetName"/> is not one Excel opens; the sheet's look has
	/// a table; <see cref="ExcelTemplateOptions.Excel"/>'s <see cref="CsvWriterOptions.HasHeader"/> is off; or
	/// <paramref name="stream"/> can't be written.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with; <typeparamref name="T"/> has no record
	/// writer, its Excel attributes are not valid, or its <see cref="ExcelSheetAttribute"/> has a table; or
	/// <see cref="ExcelSheetOptions.Columns"/> names a column that <typeparamref name="T"/>'s header doesn't have.
	/// </exception>
	/// <exception cref="IOException">The stream can't be written.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask CreateAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(Stream stream, ExcelTemplateOptions? options = null, CancellationToken cancellationToken = default) =>
		CreateCoreAsync<T>(stream, options, true, cancellationToken);

	/// <summary>Writes a template to a file, synchronously or not.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the workbook is written and closed.</returns>
	private static async ValueTask CreateCoreAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string path, ExcelTemplateOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(path);
		var (settings, name, look) = Prepare<T>(options);
		cancellationToken.ThrowIfCancellationRequested();
		var workbook = ExcelWorkbookWriter.ToFile(path, settings.Excel, async ? FileOptions.Asynchronous : FileOptions.None);
		await WriteAsync<T>(workbook, name, look, path, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Writes a template to a stream, synchronously or not.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="stream">The stream.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the workbook is written.</returns>
	private static async ValueTask CreateCoreAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(Stream stream, ExcelTemplateOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var (settings, name, look) = Prepare<T>(options);
		cancellationToken.ThrowIfCancellationRequested();
		var workbook = ExcelWorkbookWriter.Create(stream, settings.Excel, leaveOpen: true);
		await WriteAsync<T>(workbook, name, look, null, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Writes the template's sheet and disposes the workbook writer, which completes the workbook. When either fails, the
	/// writer is disposed without its failure hiding the first, and its file, if it has one, is deleted.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="workbook">The workbook writer, which this owns.</param>
	/// <param name="name">The sheet's name.</param>
	/// <param name="look">How the sheet looks, validated and protected.</param>
	/// <param name="path">The path of the workbook's file, or <see langword="null"/> for a stream.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the workbook writer is disposed.</returns>
	private static async ValueTask WriteAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelWorkbookWriter workbook, string name, ExcelSheetOptions look, string? path, bool async, CancellationToken cancellationToken)
	{
		try
		{
			if (async)
			{
				await workbook.WriteTemplateAsync<T>(name, look, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				workbook.WriteTemplate<T>(name, look);
			}

			await ExcelConvert.DisposeAsync(workbook, async).ConfigureAwait(false);
		}
		catch
		{
			try
			{
				await ExcelConvert.DisposeAsync(workbook, async).ConfigureAwait(false);
			}
			catch
			{
				// The first failure says why.
			}

			if (path is not null)
			{
				ExcelConvert.Delete(path);
			}

			throw;
		}
	}

	/// <summary>
	/// Checks the version of CsvHelper, the options and <typeparamref name="T"/>'s Excel attributes, before anything is
	/// written; and resolves the sheet's name and look.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="options">The options, or <see langword="null"/> for the default.</param>
	/// <returns>The options to use, the sheet's name, and its look, validated and protected.</returns>
	private static (ExcelTemplateOptions Settings, string Name, ExcelSheetOptions Look) Prepare<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelTemplateOptions? options)
	{
		VersionGuard.EnsureMatch();
		var settings = options ?? ExcelTemplateOptions.Default;
		settings.Validate(nameof(options));
		var members = MemberFormats.For<T>();
		if (members?.Problem is { } attributes)
		{
			throw new InvalidOperationException(attributes);
		}

		ExcelSheetOptions look;
		if (settings.Sheet is { } sheet)
		{
			look = sheet;
		}
		else if (members?.Sheet is { } attribute)
		{
			if (attribute.Table is not null)
			{
				throw new InvalidOperationException($"The [ExcelSheet] of {typeof(T).Name}: {ExcelTemplateOptions.TableProblem}");
			}

			look = attribute;
		}
		else
		{
			if (settings.Excel.Sheets.Table is not null)
			{
				throw new ArgumentException("ExcelWriterOptions.Sheets: " + ExcelTemplateOptions.TableProblem, nameof(options));
			}

			look = settings.Excel.Sheets;
		}

		return (settings, settings.SheetName ?? SheetNameOf(typeof(T)), ExcelTemplateOptions.Template(look));
	}

	/// <summary>A record type's name as a sheet's name: without a generic type's arity, made one Excel opens.</summary>
	/// <param name="type">The type.</param>
	/// <returns>The sheet's name.</returns>
	internal static string SheetNameOf(Type type)
	{
		var name = type.Name;
		var arity = name.IndexOf('`');

		return ConversionNames.SheetNameOf(arity > 0 ? name.Substring(0, arity) : name);
	}
}
