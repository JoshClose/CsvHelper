using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>A sheet as the workbook part lists it, in tab order.</summary>
internal sealed class WorkbookSheet
{
	/// <summary>Creates the sheet.</summary>
	/// <param name="name">The sheet's name.</param>
	/// <param name="index">The sheet's 0-based place in tab order.</param>
	/// <param name="visibility">Whether the sheet shows as a tab.</param>
	/// <param name="kind">What the sheet is.</param>
	/// <param name="partName">The sheet's part, normalized; <see langword="null"/> when it has none to read.</param>
	/// <param name="problem">Why the sheet has no part, as a clause, for the error when it is opened.</param>
	public WorkbookSheet(string name, int index, ExcelSheetVisibility visibility, ExcelSheetKind kind, string? partName, string? problem)
	{
		Name = name;
		Index = index;
		Visibility = visibility;
		Kind = kind;
		PartName = partName;
		Problem = problem;
	}

	/// <summary>The sheet's name.</summary>
	public string Name { get; }

	/// <summary>The sheet's 0-based place in tab order.</summary>
	public int Index { get; }

	/// <summary>Whether the sheet shows as a tab.</summary>
	public ExcelSheetVisibility Visibility { get; }

	/// <summary>What the sheet is, from its relationship's type.</summary>
	public ExcelSheetKind Kind { get; }

	/// <summary>The sheet's part, normalized; <see langword="null"/> when it has none to read (see <see cref="Problem"/>).</summary>
	public string? PartName { get; }

	/// <summary>Why <see cref="PartName"/> is <see langword="null"/>, as a clause, such as "its part xl/worksheets/sheet2.xml is missing".</summary>
	public string? Problem { get; }
}

/// <summary>
/// The workbook part of a package and what it names: the sheets in tab order, the 1904 date system, and the styles and
/// shared strings parts. Loading it reads the content types, the package's relationships, the workbook part and its
/// relationships.
/// </summary>
/// <remarks>
/// <para>
/// The workbook part is the target of the package's officeDocument relationship, or <c>xl/workbook.xml</c> when the
/// package has no relationships part. When the package has content types, the workbook part's must be a workbook's
/// (<see cref="ContentTypes.IsWorkbook"/>): an <c>.xlsb</c>'s throws <see cref="NotSupportedException"/>, and another
/// package's, such as a <c>.docx</c>'s, <see cref="InvalidDataException"/>.
/// </para>
/// <para>
/// Relationship types are matched by their last segment (<see cref="Relationship.TypeName"/>), so the transitional and the
/// strict URIs both match. A sheet whose part can't be found is still listed, in its place; opening it throws.
/// </para>
/// </remarks>
internal sealed class WorkbookPart
{
	private readonly List<WorkbookSheet> sheets = [];

	/// <summary>Creates the workbook part.</summary>
	/// <param name="partName">The workbook part's name.</param>
	private WorkbookPart(string partName) => PartName = partName;

	/// <summary>The workbook part's name, such as <c>xl/workbook.xml</c>.</summary>
	public string PartName { get; }

	/// <summary>Whether the workbook uses the 1904 date system (<c>workbookPr/@date1904</c>), whose serial 0 is January 1, 1904.</summary>
	public bool Date1904 { get; private set; }

	/// <summary>The sheets, in tab order: worksheets, chartsheets and the legacy kinds.</summary>
	public IReadOnlyList<WorkbookSheet> Sheets => sheets;

	/// <summary>The styles part, or <see langword="null"/> when the workbook names none.</summary>
	public string? StylesPart { get; private set; }

	/// <summary>The shared strings part, or <see langword="null"/> when the workbook names none.</summary>
	public string? SharedStringsPart { get; private set; }

	/// <summary>Loads the workbook part of a package.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the workbook part.</returns>
	/// <exception cref="InvalidDataException">
	/// The package has no workbook part or is not a spreadsheet, or a part can't be read or passes a limit.
	/// </exception>
	/// <exception cref="NotSupportedException">The workbook is an <c>.xlsb</c>.</exception>
	public static async ValueTask<WorkbookPart> LoadAsync(PackageArchive archive, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var contentTypes = await ContentTypes.LoadAsync(archive, limits, async, cancellationToken).ConfigureAwait(false);
		var packageRelationships = await Relationships.LoadAsync(archive, "", limits, async, cancellationToken).ConfigureAwait(false);
		var partName = Excel.PartName.DefaultWorkbook;
		if (packageRelationships is not null)
		{
			partName = packageRelationships.FindType("officeDocument")?.Target ??
				throw new InvalidDataException($"The package has no workbook: {packageRelationships.Name} has no officeDocument relationship.");
		}

		var contentType = contentTypes?.Of(partName);
		if (contentType is not null && !ContentTypes.IsWorkbook(contentType))
		{
			if (ContentTypes.Is(contentType, ContentTypes.BinaryWorkbook))
			{
				throw new NotSupportedException("The workbook is an Excel binary workbook (.xlsb), which CsvHelper.Excel doesn't read.");
			}

			throw new InvalidDataException($"The package is not a spreadsheet: its main part, {partName}, has the content type {contentType}.");
		}

		if (!archive.Contains(partName))
		{
			throw new InvalidDataException($"The package has no workbook: its workbook part, {partName}, is missing.");
		}

		var relationships = await Relationships.LoadAsync(archive, partName, limits, async, cancellationToken).ConfigureAwait(false);
		var workbook = new WorkbookPart(partName)
		{
			StylesPart = relationships?.FindType("styles")?.Target,
			SharedStringsPart = relationships?.FindType("sharedStrings")?.Target,
		};
		await workbook.ReadAsync(archive, relationships, limits, async, cancellationToken).ConfigureAwait(false);

		return workbook;
	}

	/// <summary>The kind of sheet a relationship's type names.</summary>
	/// <param name="typeName">The last segment of the relationship's type.</param>
	/// <returns>The kind, or <see langword="null"/> when the type is not a sheet's.</returns>
	private static ExcelSheetKind? KindOf(string typeName) => typeName switch
	{
		"worksheet" => ExcelSheetKind.Worksheet,
		"chartsheet" => ExcelSheetKind.Chartsheet,
		"dialogsheet" => ExcelSheetKind.DialogSheet,
		"xlMacrosheet" or "xlIntlMacrosheet" => ExcelSheetKind.MacroSheet,
		_ => null,
	};

	/// <summary>Reads the workbook part: <c>workbookPr</c> and <c>sheets</c>.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="relationships">The workbook part's relationships, or <see langword="null"/> when it has none.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task that completes when the part is read.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, or passes a limit.</exception>
	private async ValueTask ReadAsync(PackageArchive archive, Relationships? relationships, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var scanner = await archive.OpenXmlAsync(PartName, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("workbook"u8))
			{
				throw scanner.Error("its root element is not workbook");
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				if (scanner.IsStartTag("workbookPr"u8))
				{
					var attributes = scanner.Attributes;
					while (attributes.MoveNext())
					{
						if (attributes.IsName("date1904"u8))
						{
							Date1904 = attributes.GetBoolean();
						}
					}
				}
				else if (scanner.IsStartTag("sheets"u8))
				{
					var sheetsDepth = scanner.ElementDepth;
					while (await scanner.ReadChildAsync(sheetsDepth, async, cancellationToken).ConfigureAwait(false))
					{
						if (scanner.IsStartTag("sheet"u8))
						{
							sheets.Add(ReadSheet(scanner, relationships, archive));
						}

						await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
					}

					continue;
				}

				await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}
	}

	/// <summary>Reads a <c>sheet</c> element: its name, state and relationship, and from that its kind and part.</summary>
	/// <param name="scanner">The scanner, at the element's start tag.</param>
	/// <param name="relationships">The workbook part's relationships, or <see langword="null"/>.</param>
	/// <param name="archive">The package, to see whether the sheet's part is there.</param>
	/// <returns>The sheet.</returns>
	/// <exception cref="InvalidDataException">The sheet has no name, or a state that is not one of Excel's.</exception>
	private WorkbookSheet ReadSheet(XmlScanner scanner, Relationships? relationships, PackageArchive archive)
	{
		string? name = null;
		string? id = null;
		var visibility = ExcelSheetVisibility.Visible;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("name"u8))
			{
				name = attributes.DecodeValue();
			}
			else if (attributes.IsName("id"u8))
			{
				// r:id, matched by its local name with any prefix.
				id = attributes.DecodeValue();
			}
			else if (attributes.IsName("state"u8))
			{
				visibility = attributes.DecodeValue() switch
				{
					"visible" => ExcelSheetVisibility.Visible,
					"hidden" => ExcelSheetVisibility.Hidden,
					"veryHidden" => ExcelSheetVisibility.VeryHidden,
					_ => throw attributes.InvalidValue("a sheet state (visible, hidden or veryHidden)"),
				};
			}
		}

		if (name is null)
		{
			throw scanner.Error("a sheet has no name");
		}

		var index = sheets.Count;
		if (id is null)
		{
			return new WorkbookSheet(name, index, visibility, ExcelSheetKind.Worksheet, null, "it has no relationship id (r:id)");
		}

		var relationship = relationships?.Find(id);
		if (relationship is null)
		{
			var where = relationships?.Name ?? Excel.PartName.RelationshipsOf(PartName);

			return new WorkbookSheet(name, index, visibility, ExcelSheetKind.Worksheet, null, $"its relationship {id} is missing from {where}");
		}

		var kind = KindOf(relationship.TypeName);
		if (kind is null)
		{
			return new WorkbookSheet(name, index, visibility, ExcelSheetKind.Worksheet, null, $"its relationship {id} has the type {relationship.Type}, which is not a sheet's");
		}

		if (relationship.Target is null)
		{
			return new WorkbookSheet(name, index, visibility, kind.Value, null, $"its relationship {id} is to a file outside the workbook");
		}

		if (!archive.Contains(relationship.Target))
		{
			return new WorkbookSheet(name, index, visibility, kind.Value, null, $"its part {relationship.Target} is missing");
		}

		return new WorkbookSheet(name, index, visibility, kind.Value, relationship.Target, null);
	}
}
