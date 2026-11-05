using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Walks the elements of a part with an <see cref="XmlScanner"/>, for the package parts' parsers. Each method reads
/// synchronously or asynchronously as <c>async</c> says, so that one parser serves both: synchronously, every task it
/// returns has completed (<see cref="SyncOrAsync"/>).
/// </summary>
/// <remarks>
/// The pattern: read the root (<see cref="ReadRootAsync"/>), then, for an element at depth <c>d</c>, read its children
/// with <see cref="ReadChildAsync"/> and end each child with <see cref="SkipElementAsync"/>, or read the child's own
/// children the same way. A child left unskipped would make its children look like siblings.
/// </remarks>
internal static class XmlElements
{
	/// <summary>Reads the next token, refilling as needed.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="async">Whether to read the part's stream asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is <see langword="false"/> at the end of the part.</returns>
	public static ValueTask<bool> ReadAsync(this XmlScanner scanner, bool async, CancellationToken cancellationToken) =>
		async ? scanner.ReadAsync(cancellationToken) : new ValueTask<bool>(scanner.Read());

	/// <summary>Reads up to the start tag of the root element, past the XML declaration, comments and white space.</summary>
	/// <param name="scanner">The scanner, at the start of the part.</param>
	/// <param name="async">Whether to read the part's stream asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task that completes at the root's start tag.</returns>
	/// <exception cref="System.IO.InvalidDataException">The XML can't be read, or has no root element.</exception>
	public static async ValueTask ReadRootAsync(this XmlScanner scanner, bool async, CancellationToken cancellationToken)
	{
		while (await scanner.ReadAsync(async, cancellationToken).ConfigureAwait(false))
		{
			if (scanner.Kind == XmlTokenKind.StartTag)
			{
				return;
			}
		}

		// The scanner throws first, at the end of a part without one.
		throw scanner.Error("it has no root element");
	}

	/// <summary>
	/// Reads up to the next child of the element at <paramref name="parentDepth"/>: after the parent's start tag, or after a
	/// child that was skipped. Text and CDATA between children are passed over.
	/// </summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="parentDepth">The depth of the parent element (<see cref="XmlScanner.ElementDepth"/> of its start tag).</param>
	/// <param name="async">Whether to read the part's stream asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>
	/// A task whose result is <see langword="true"/> at a child's start tag, and <see langword="false"/> after the parent's
	/// end tag (at once for an empty parent).
	/// </returns>
	/// <exception cref="System.IO.InvalidDataException">The XML can't be read.</exception>
	public static async ValueTask<bool> ReadChildAsync(this XmlScanner scanner, int parentDepth, bool async, CancellationToken cancellationToken)
	{
		while (scanner.Depth >= parentDepth && await scanner.ReadAsync(async, cancellationToken).ConfigureAwait(false))
		{
			if (scanner.Kind == XmlTokenKind.StartTag)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Disposes a scanner, and so its part's stream, synchronously or not.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <returns>A task that completes when it is disposed.</returns>
	public static ValueTask DisposeAsync(this XmlScanner scanner, bool async)
	{
		if (async)
		{
			return scanner.DisposeAsync();
		}

		scanner.Dispose();

		return default;
	}

	/// <summary>
	/// Reads the text of the element whose start tag was just read, up to and including its end tag: its text and CDATA,
	/// decoded as XML (<see cref="XmlScanner.DecodeContent"/>) and joined; elements in it are skipped with their text. The
	/// <c>_xHHHH_</c> escapes and white space are left to the caller.
	/// </summary>
	/// <param name="scanner">The scanner, at a start tag.</param>
	/// <param name="async">Whether to read the part's stream asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the text, as UTF-8; empty for an empty element.</returns>
	/// <exception cref="System.IO.InvalidDataException">The XML can't be read.</exception>
	public static async ValueTask<byte[]> ReadTextAsync(this XmlScanner scanner, bool async, CancellationToken cancellationToken)
	{
		if (scanner.Kind != XmlTokenKind.StartTag || scanner.IsEmptyElement)
		{
			return [];
		}

		var depth = scanner.Depth;
		var text = new byte[64];
		var length = 0;
		while (await scanner.ReadAsync(async, cancellationToken).ConfigureAwait(false))
		{
			switch (scanner.Kind)
			{
				case XmlTokenKind.Text:
				case XmlTokenKind.CData:
					if (text.Length - length < scanner.Content.Length)
					{
						Array.Resize(ref text, Math.Max(text.Length * 2, length + scanner.Content.Length));
					}

					length += scanner.DecodeContent(text.AsSpan(length));

					break;
				case XmlTokenKind.StartTag:
					await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);

					break;
				case XmlTokenKind.EndTag:
					if (scanner.Depth < depth)
					{
						Array.Resize(ref text, length);

						return text;
					}

					break;
			}
		}

		// The scanner throws first, at the end of a part inside an element.
		throw scanner.Error("it ends inside an element");
	}

	/// <summary>Skips the content and end tag of the element whose start tag was just read; nothing for an empty element.</summary>
	/// <param name="scanner">The scanner, at a start tag.</param>
	/// <param name="async">Whether to read the part's stream asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task that completes after the element's end tag.</returns>
	/// <exception cref="System.IO.InvalidDataException">The XML can't be read.</exception>
	public static ValueTask SkipElementAsync(this XmlScanner scanner, bool async, CancellationToken cancellationToken)
	{
		if (scanner.Kind != XmlTokenKind.StartTag || scanner.IsEmptyElement)
		{
			return default;
		}

		var depth = scanner.Depth - 1;
		if (async)
		{
			return scanner.SkipToAsync(depth, cancellationToken);
		}

		scanner.SkipTo(depth);

		return default;
	}
}
