using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>A relationship from a part (or the package) to another part.</summary>
internal sealed class Relationship
{
	/// <summary>Creates the relationship.</summary>
	/// <param name="id">The relationship's id, such as <c>rId1</c>.</param>
	/// <param name="type">The relationship's type, a URI.</param>
	/// <param name="target">The target's part name, resolved and normalized; <see langword="null"/> for an external target.</param>
	public Relationship(string id, string type, string? target)
	{
		Id = id;
		Type = type;
		Target = target;
	}

	/// <summary>The relationship's id, such as <c>rId1</c>.</summary>
	public string Id { get; }

	/// <summary>The relationship's type, a URI such as <c>http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet</c>.</summary>
	public string Type { get; }

	/// <summary>
	/// The last segment of <see cref="Type"/>, such as <c>worksheet</c>, which is the same in the transitional and the
	/// strict type URIs.
	/// </summary>
	public string TypeName => Type.Substring(Type.LastIndexOf('/') + 1);

	/// <summary>The target's part name, resolved and normalized; <see langword="null"/> for an external target.</summary>
	public string? Target { get; }
}

/// <summary>The relationships of a part, or of the package, from its relationships part (<c>*.rels</c>).</summary>
internal sealed class Relationships
{
	private readonly List<Relationship> all = [];
	private readonly Dictionary<string, Relationship> byId = new(StringComparer.Ordinal);

	/// <summary>Creates relationships with a name for errors.</summary>
	/// <param name="partName">The relationships part, such as <c>xl/_rels/workbook.xml.rels</c>.</param>
	private Relationships(string partName) => Name = partName;

	/// <summary>The relationships part they are from, such as <c>xl/_rels/workbook.xml.rels</c>.</summary>
	public string Name { get; }

	/// <summary>The relationships, in order.</summary>
	public IReadOnlyList<Relationship> All => all;

	/// <summary>The relationship with an id (compared exactly, as XML ids are).</summary>
	/// <param name="id">The id.</param>
	/// <returns>The relationship, or <see langword="null"/>.</returns>
	public Relationship? Find(string id) => byId.TryGetValue(id, out var relationship) ? relationship : null;

	/// <summary>The first relationship of a type with an internal target.</summary>
	/// <param name="typeName">The last segment of the type, such as <c>styles</c> (<see cref="Relationship.TypeName"/>).</param>
	/// <returns>The relationship, or <see langword="null"/>.</returns>
	public Relationship? FindType(string typeName)
	{
		foreach (var relationship in all)
		{
			if (relationship.Target is not null && relationship.TypeName == typeName)
			{
				return relationship;
			}
		}

		return null;
	}

	/// <summary>Reads the relationships of a part, if it has a relationships part.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="source">The part, normalized; an empty string for the package's own relationships (<c>_rels/.rels</c>).</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the relationships, or <see langword="null"/> when the part has no relationships part.</returns>
	/// <exception cref="InvalidDataException">The relationships part can't be read, or passes a limit.</exception>
	public static async ValueTask<Relationships?> LoadAsync(PackageArchive archive, string source, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var partName = source.Length == 0 ? PartName.PackageRelationships : PartName.RelationshipsOf(source);
		if (!archive.Contains(partName))
		{
			return null;
		}

		var relationships = new Relationships(partName);
		var scanner = await archive.OpenXmlAsync(partName, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("Relationships"u8))
			{
				throw scanner.Error("its root element is not Relationships");
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				if (scanner.IsStartTag("Relationship"u8))
				{
					string? id = null;
					string? type = null;
					string? target = null;
					var external = false;
					var attributes = scanner.Attributes;
					while (attributes.MoveNext())
					{
						if (attributes.IsName("Id"u8))
						{
							id = attributes.DecodeValue();
						}
						else if (attributes.IsName("Type"u8))
						{
							type = attributes.DecodeValue();
						}
						else if (attributes.IsName("Target"u8))
						{
							target = attributes.DecodeValue();
						}
						else if (attributes.IsName("TargetMode"u8))
						{
							external = attributes.DecodeValue() == "External";
						}
					}

					// A relationship without an id, type or target says nothing; the first of two with one id counts.
					if (id is not null && type is not null && target is not null)
					{
						var relationship = new Relationship(id, type, external ? null : PartName.Resolve(source, target));
						if (relationships.byId.TryAdd(id, relationship))
						{
							relationships.all.Add(relationship);
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

		return relationships;
	}
}
