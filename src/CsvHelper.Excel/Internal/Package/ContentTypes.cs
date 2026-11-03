using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The content types of a package's parts, from <c>[Content_Types].xml</c>: an <c>Override</c> for a part by name, or else
/// a <c>Default</c> for its extension. Content types compare ignoring case, as MIME types do.
/// </summary>
internal sealed class ContentTypes
{
	/// <summary>The main part of an <c>.xlsx</c> workbook.</summary>
	public const string Workbook = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";

	/// <summary>The main part of an <c>.xltx</c> template.</summary>
	public const string Template = "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml";

	/// <summary>The main part of an <c>.xlsm</c> macro-enabled workbook.</summary>
	public const string MacroEnabledWorkbook = "application/vnd.ms-excel.sheet.macroEnabled.main+xml";

	/// <summary>The main part of an <c>.xltm</c> macro-enabled template.</summary>
	public const string MacroEnabledTemplate = "application/vnd.ms-excel.template.macroEnabled.main+xml";

	/// <summary>The main part of an <c>.xlam</c> add-in, a macro-enabled workbook too.</summary>
	public const string AddIn = "application/vnd.ms-excel.addin.macroEnabled.main+xml";

	/// <summary>The main part of an <c>.xlsb</c> binary workbook, which is not XML.</summary>
	public const string BinaryWorkbook = "application/vnd.ms-excel.sheet.binary.macroEnabled.main";

	private readonly Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> overrides = new(PartName.Comparer);

	/// <summary>Whether a content type is the main part of a workbook this package reads: a workbook, template or add-in, macro-enabled or not.</summary>
	/// <param name="contentType">The content type, without parameters.</param>
	/// <returns>Whether it is a workbook's main part.</returns>
	public static bool IsWorkbook(string contentType) =>
		Is(contentType, Workbook) || Is(contentType, Template) || Is(contentType, MacroEnabledWorkbook) ||
		Is(contentType, MacroEnabledTemplate) || Is(contentType, AddIn);

	/// <summary>Whether two content types are the same, ignoring case.</summary>
	/// <param name="contentType">A content type.</param>
	/// <param name="other">The other content type.</param>
	/// <returns>Whether they are the same.</returns>
	public static bool Is(string contentType, string other) => string.Equals(contentType, other, StringComparison.OrdinalIgnoreCase);

	/// <summary>The content type of a part: its override, or the default for its extension, without parameters.</summary>
	/// <param name="partName">The part's name, normalized.</param>
	/// <returns>The content type, or <see langword="null"/> when the part has none.</returns>
	public string? Of(string partName)
	{
		if (!overrides.TryGetValue(partName, out var contentType) && !defaults.TryGetValue(PartName.Extension(partName), out contentType))
		{
			return null;
		}

		var parameters = contentType.IndexOf(';');

		return (parameters < 0 ? contentType : contentType.Substring(0, parameters)).Trim();
	}

	/// <summary>Reads the content types part, if the package has one.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the content types, or <see langword="null"/> when the package has no such part.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, or passes a limit.</exception>
	public static async ValueTask<ContentTypes?> LoadAsync(PackageArchive archive, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		if (!archive.Contains(PartName.ContentTypes))
		{
			return null;
		}

		var types = new ContentTypes();
		var scanner = await archive.OpenXmlAsync(PartName.ContentTypes, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("Types"u8))
			{
				throw scanner.Error("its root element is not Types");
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				var isDefault = scanner.IsStartTag("Default"u8);
				if (isDefault || scanner.IsStartTag("Override"u8))
				{
					string? key = null;
					string? contentType = null;
					var attributes = scanner.Attributes;
					while (attributes.MoveNext())
					{
						if (attributes.IsName(isDefault ? "Extension"u8 : "PartName"u8))
						{
							key = attributes.DecodeValue();
						}
						else if (attributes.IsName("ContentType"u8))
						{
							contentType = attributes.DecodeValue();
						}
					}

					// One without its attributes says nothing; the first of two for the same key counts.
					if (key is not null && contentType is not null)
					{
						if (isDefault)
						{
							types.defaults.TryAdd(key.TrimStart('.'), contentType);
						}
						else
						{
							types.overrides.TryAdd(PartName.Normalize(key), contentType);
						}
					}
				}

				await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}

		return types;
	}
}
