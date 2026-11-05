using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Reads a worksheet's <c>&lt;dimension ref&gt;</c>, the used range its producer states, from the start of its part: the
/// root and its children up to <c>dimension</c>, or up to <c>sheetData</c> when it has none. Nothing after that is read, so
/// a few KB are inflated whatever the sheet's size.
/// </summary>
internal static class SheetDimension
{
	/// <summary>Reads a worksheet's stated used range.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="partName">The worksheet's part, normalized.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>
	/// A task whose result is the range, or <see langword="null"/> when the part's root is not a worksheet, it states no
	/// range before its rows, or the range is not a block of cells.
	/// </returns>
	/// <exception cref="InvalidDataException">The part is missing, can't be inflated or read, or passes a limit.</exception>
	public static async ValueTask<ExcelRange?> ReadAsync(PackageArchive archive, string partName, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var scanner = await archive.OpenXmlAsync(partName, false, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("worksheet"u8))
			{
				return null;
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				if (scanner.IsStartTag("dimension"u8))
				{
					var attributes = scanner.Attributes;
					while (attributes.MoveNext())
					{
						if (attributes.IsName("ref"u8))
						{
							return AreaReference.TryParseArea(attributes.DecodeValue().AsSpan(), out var range) ? range : null;
						}
					}

					return null;
				}

				if (scanner.IsStartTag("sheetData"u8))
				{
					return null;
				}

				await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
			}

			return null;
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}
	}
}
