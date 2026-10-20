using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace CsvHelper.Docs.Api;

/// <summary>
/// The XML documentation file of an assembly, for <see cref="MetadataReference.CreateFromFile"/>. (Roslyn's own
/// implementation is in the Workspaces package.)
/// </summary>
internal sealed class XmlDocs : DocumentationProvider
{
	private readonly string path;
	private readonly Dictionary<string, string> members;

	private XmlDocs(string path, Dictionary<string, string> members)
	{
		this.path = path;
		this.members = members;
	}

	public static XmlDocs Load(string path)
	{
		var members = XDocument.Load(path).Descendants("member")
			.Where(m => m.Attribute("name") is not null)
			.GroupBy(m => m.Attribute("name")!.Value)
			.ToDictionary(g => g.Key, g => g.First().ToString(SaveOptions.DisableFormatting));

		return new XmlDocs(path, members);
	}

	protected override string? GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default) =>
		members.GetValueOrDefault(documentationMemberID);

	public override bool Equals(object? obj) => obj is XmlDocs other && string.Equals(other.path, path, StringComparison.OrdinalIgnoreCase);

	public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(path);
}
