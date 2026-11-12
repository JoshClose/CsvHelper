using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The links of the sheet being written (<see cref="ExcelWriterOptions.Links"/>): each link's cell and target, and a
/// relationship for each distinct target, numbered in the order the targets are first used. The sheet writes the links'
/// <c>hyperlinks</c> after its rows, and their relationships, with its table's, in its relationships part.
/// </summary>
/// <remarks>
/// <para>
/// A field's text is a link's target when <see cref="Target"/> takes it: an absolute URI's <see cref="Uri.AbsoluteUri"/>,
/// whose scheme <see cref="ExcelLinks.Web"/> limits to <c>http</c>, <c>https</c> and <c>mailto</c>; or, with
/// <see cref="ExcelLinks.All"/>, a relative URI's text. Excel takes them as these rules say (checked through COM on
/// 2026-11-08 and 2026-11-10; <c>Csv\_bench\results\phase5\design</c> and <c>step5</c>):
/// </para>
/// <list type="bullet">
/// <item>
/// A target is <c>TargetMode="External"</c>, which Excel refuses a link's relationship without; several links may share
/// one. Excel splits a fragment (<c>#frag</c>) into the link's sub-address itself.
/// </item>
/// <item>
/// Excel reads a target's escapes byte by byte: <c>file:///C:/Temp/r%C3%A9port.txt</c> is a file whose name has U+00C3
/// and U+00A9 where the URI has one U+00E9, and a relative target keeps <c>%C3%A9</c> as written. So the escapes of a
/// character past ASCII that a target shows as itself are written as the character, which Excel takes in every target.
/// <c>%20</c> and the other escapes stay as they are.
/// </item>
/// <item>A target has at most 8,192 UTF-16 code units, its fragment and its escapes as written included: Excel refuses a longer one.</item>
/// <item>A sheet has at most 65,530 links, whether they share relationships or not: Excel refuses a sheet with more.</item>
/// </list>
/// <para>
/// A text that isn't a link's target, or comes past the sheet's 65,530 links, is a text cell without a link.
/// </para>
/// <para>
/// The distinct targets are held until the sheet ends, since its relationships part comes after it: each once, about
/// 16,450 bytes for one of 8,192 code units, so 1.0 GiB for Excel's worst case, 65,530 of them (measured at Phase 5's
/// step 7: <c>LinkMemoryTests</c>). A record discarded takes the targets it added with it, so that records that fail,
/// whose links the cap doesn't count, can't hold more.
/// </para>
/// </remarks>
internal sealed class SheetLinks
{
	/// <summary>The most links Excel opens in a sheet.</summary>
	public const int MaxLinks = 65_530;

	/// <summary>The most UTF-16 code units Excel opens in a link's target.</summary>
	public const int MaxTargetLength = 8_192;

	// Each distinct target, by its index, and each one's index.
	private readonly List<string> targets = [];
	private readonly Dictionary<string, int> indexes = new(StringComparer.Ordinal);
	// The links, in the order of their cells.
	private readonly List<Link> links = [];
	// The links and the targets of the records ended: those after them are the current record's.
	private int kept;
	private int keptTargets;
	// The targets that the links use, in the order of their relationships' ids, from the first id; once the links are written.
	private List<int>? numbered;
	private int firstId;

	/// <summary>The number of links, the current record's included.</summary>
	public int Count => links.Count;

	/// <summary>The number of distinct targets held, the current record's included.</summary>
	public int Targets => targets.Count;

	/// <summary>Whether the sheet has as many links as Excel opens: a later URI is text without one.</summary>
	public bool IsFull => links.Count >= MaxLinks;

	/// <summary>The number of relationships the links use, once they are written (<see cref="WriteHyperlinks"/>).</summary>
	public int Relationships => numbered?.Count ?? 0;

	/// <summary>
	/// A link's target for a URI's text, when the policy makes it a link: an absolute URI's <see cref="Uri.AbsoluteUri"/>,
	/// or a relative one's text with its control characters escaped; with the escapes of characters past ASCII written as
	/// the characters (see the remarks).
	/// </summary>
	/// <param name="text">The URI's text, as the writer wrote its field.</param>
	/// <param name="policy">Which URIs are links.</param>
	/// <returns>The target, or <see langword="null"/> when the text is no link: the policy doesn't take it, it isn't a URI, or its target is longer than Excel opens.</returns>
	public static string? Target(string text, ExcelLinks policy)
	{
		if (policy == ExcelLinks.None)
		{
			return null;
		}

		var trimmed = text.Trim();
		if (trimmed.Length == 0 || !Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri))
		{
			return null;
		}

		string target;
		if (uri.IsAbsoluteUri)
		{
			if (policy == ExcelLinks.Web && uri.Scheme is not ("http" or "https" or "mailto"))
			{
				return null;
			}

			target = uri.AbsoluteUri;
		}
		else
		{
			if (policy != ExcelLinks.All)
			{
				return null;
			}

			target = EscapeControls(uri.OriginalString);
		}

		target = UnescapePastAscii(target);

		return target.Length <= MaxTargetLength ? target : null;
	}

	/// <summary>Adds a link of the current record.</summary>
	/// <param name="row">Its cell's row, from 1.</param>
	/// <param name="column">Its cell's column, from 0.</param>
	/// <param name="target">Its target (<see cref="Target"/>).</param>
	public void Add(long row, int column, string target)
	{
		if (!indexes.TryGetValue(target, out var index))
		{
			index = targets.Count;
			targets.Add(target);
			indexes.Add(target, index);
		}

		links.Add(new Link((int)row, column, index));
	}

	/// <summary>Keeps the current record's links and targets: the record has ended.</summary>
	public void EndRecord()
	{
		kept = links.Count;
		keptTargets = targets.Count;
	}

	/// <summary>
	/// Drops the current record's links, and the targets it added: the record was discarded. The records before it use
	/// none of those targets, which came after theirs.
	/// </summary>
	public void DiscardRecord()
	{
		links.RemoveRange(kept, links.Count - kept);
		for (var i = keptTargets; i < targets.Count; i++)
		{
			indexes.Remove(targets[i]);
		}

		targets.RemoveRange(keptTargets, targets.Count - keptTargets);
	}

	/// <summary>
	/// Writes the links' <c>hyperlinks</c>, each cell's with the id of its target's relationship. The targets the links
	/// use get ids from the first one, in the order of their first link.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="firstId">The first relationship's id: 1, or 2 after a table's.</param>
	public void WriteHyperlinks(XmlOutput xml, int firstId)
	{
		this.firstId = firstId;
		numbered = [];
		var ids = new int[targets.Count];
		xml.Append("<hyperlinks>"u8);
		foreach (var link in links)
		{
			var id = ids[link.Target];
			if (id == 0)
			{
				id = ids[link.Target] = firstId + numbered.Count;
				numbered.Add(link.Target);
			}

			xml.Append("<hyperlink ref=\""u8);
			xml.AppendReference(SheetSink.ColumnName(link.Column), link.Row);
			xml.Append("\" r:id=\"rId"u8);
			xml.AppendNumber(id);
			xml.Append("\"/>"u8);
		}

		xml.Append("</hyperlinks>"u8);
	}

	/// <summary>Writes a relationship of the links, written before (<see cref="WriteHyperlinks"/>): its id, and its target, external.</summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="index">The relationship's place, from 0, below <see cref="Relationships"/>.</param>
	public void AppendRelationship(XmlOutput xml, int index)
	{
		xml.Append("<Relationship Id=\"rId"u8);
		xml.AppendNumber(firstId + index);
		xml.Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\""u8);
		// A relationship's target is XML's text alone: a part of relationships has no _xHHHH_ escapes.
		var target = targets[numbered![index]];
		xml.Append(Encoding.UTF8.GetBytes(target.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;")));
		xml.Append("\" TargetMode=\"External\"/>"u8);
	}

	/// <summary>
	/// Escapes the characters of a relative URI's text that a relationship's target can't hold: C0 controls, which XML
	/// refuses or reads as spaces, DEL, and the non-characters U+FFFE and U+FFFF, each as the escapes of its UTF-8 bytes.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The text, escaped.</returns>
	private static string EscapeControls(string text)
	{
		var builder = default(StringBuilder);
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c >= ' ' && c != (char)0x7F && c < (char)0xFFFE)
			{
				builder?.Append(c);

				continue;
			}

			builder ??= new StringBuilder(text, 0, i, text.Length + 16);
			foreach (var b in Encoding.UTF8.GetBytes(c.ToString()))
			{
				builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
			}
		}

		return builder?.ToString() ?? text;
	}

	/// <summary>
	/// Writes the escapes of each character past ASCII that a target shows as itself as the character: its UTF-8 bytes,
	/// each <c>%XX</c>, decoded. Other escapes stay: ASCII's, which may have a meaning in a URI (<c>%2F</c>, <c>%23</c>);
	/// bytes that aren't a character's UTF-8; and characters that don't show as themselves (<see cref="ShowsAsItself"/>),
	/// such as controls and the directions' marks.
	/// </summary>
	/// <param name="target">The target.</param>
	/// <returns>The target, with those escapes decoded.</returns>
	private static string UnescapePastAscii(string target)
	{
		var at = target.IndexOf('%');
		if (at < 0)
		{
			return target;
		}

		var builder = new StringBuilder(target.Length);
		var copied = 0;
		while (at < target.Length)
		{
			if (target[at] == '%' && TryDecode(target, at, out var decoded, out var length))
			{
				builder.Append(target, copied, at - copied).Append(decoded);
				at += length;
				copied = at;
			}
			else
			{
				at++;
			}
		}

		builder.Append(target, copied, target.Length - copied);

		return builder.ToString();
	}

	/// <summary>Decodes the escapes, at a <c>%</c>, of one character past ASCII that shows as itself.</summary>
	/// <param name="text">The text.</param>
	/// <param name="at">The <c>%</c>'s place.</param>
	/// <param name="decoded">The character, as one or two UTF-16 code units.</param>
	/// <param name="length">The escapes' length: 3 for each of its 2 to 4 bytes.</param>
	/// <returns>Whether the escapes are such a character's.</returns>
	private static bool TryDecode(string text, int at, out string decoded, out int length)
	{
		decoded = "";
		length = 0;
		if (!TryByte(text, at, out var lead) || lead < 0xC2 || lead > 0xF4)
		{
			return false;
		}

		var count = lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
		var scalar = lead & (count == 2 ? 0x1F : count == 3 ? 0x0F : 0x07);
		for (var k = 1; k < count; k++)
		{
			if (!TryByte(text, at + (3 * k), out var next) || (next & 0xC0) != 0x80)
			{
				return false;
			}

			scalar = (scalar << 6) | (next & 0x3F);
		}

		// Overlong forms, surrogates and numbers past U+10FFFF are no characters.
		if ((count == 3 && scalar < 0x800) || (count == 4 && (scalar < 0x10000 || scalar > 0x10FFFF)) || (scalar >= 0xD800 && scalar <= 0xDFFF))
		{
			return false;
		}

		var character = char.ConvertFromUtf32(scalar);
		if (!ShowsAsItself(character, scalar))
		{
			return false;
		}

		decoded = character;
		length = 3 * count;

		return true;
	}

	/// <summary>Reads an escape's byte: <c>%</c> and two hexadecimal digits.</summary>
	/// <param name="text">The text.</param>
	/// <param name="at">The <c>%</c>'s place.</param>
	/// <param name="value">The byte.</param>
	/// <returns>Whether an escape is there.</returns>
	private static bool TryByte(string text, int at, out int value)
	{
		value = 0;
		if (at + 2 >= text.Length || text[at] != '%' || !Uri.IsHexDigit(text[at + 1]) || !Uri.IsHexDigit(text[at + 2]))
		{
			return false;
		}

		value = (Uri.FromHex(text[at + 1]) << 4) | Uri.FromHex(text[at + 2]);

		return true;
	}

	/// <summary>
	/// Whether a character past ASCII shows as itself in a target: not a control, a formatting character, a line or
	/// paragraph separator, a non-character, a private use character, or unassigned.
	/// </summary>
	/// <param name="character">The character, as one or two UTF-16 code units.</param>
	/// <param name="scalar">Its code point.</param>
	/// <returns>Whether it shows as itself.</returns>
	private static bool ShowsAsItself(string character, int scalar)
	{
		if ((scalar >= 0xFDD0 && scalar <= 0xFDEF) || (scalar & 0xFFFE) == 0xFFFE)
		{
			return false;
		}

		return CharUnicodeInfo.GetUnicodeCategory(character, 0) is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
			or UnicodeCategory.ParagraphSeparator or UnicodeCategory.OtherNotAssigned or UnicodeCategory.PrivateUse);
	}

	/// <summary>A link: its cell, and its target's index.</summary>
	/// <param name="Row">The cell's row, from 1.</param>
	/// <param name="Column">The cell's column, from 0.</param>
	/// <param name="Target">The target's index.</param>
	private readonly record struct Link(int Row, int Column, int Target);
}
