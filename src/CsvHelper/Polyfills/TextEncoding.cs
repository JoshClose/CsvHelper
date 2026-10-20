#if !NET
using System.Buffers;
using CsvHelper;

namespace System.Text
{
	/// <summary>The members of .NET's <c>System.Text.Ascii</c> that the library uses.</summary>
	internal static class Ascii
	{
		/// <summary>
		/// Copies chars as bytes while they are ASCII; stands in for <c>Ascii.FromUtf16</c>.
		/// See <see cref="Portable.NarrowAscii"/>.
		/// </summary>
		/// <param name="source">The chars to copy.</param>
		/// <param name="destination">The bytes to copy them to.</param>
		/// <param name="bytesWritten">The number of bytes written, which is also the number of chars copied.</param>
		/// <returns>
		/// <see cref="OperationStatus.Done"/> when all of <paramref name="source"/> was copied,
		/// <see cref="OperationStatus.DestinationTooSmall"/> when it did not fit, <see cref="OperationStatus.InvalidData"/>
		/// at the first char that is not ASCII.
		/// </returns>
		public static OperationStatus FromUtf16(ReadOnlySpan<char> source, Span<byte> destination, out int bytesWritten) =>
			Portable.NarrowAscii(source, destination, out bytesWritten);

		/// <summary>
		/// Compares ASCII bytes ignoring case; stands in for <c>Ascii.EqualsIgnoreCase(ReadOnlySpan&lt;byte&gt;, ReadOnlySpan&lt;byte&gt;)</c>.
		/// </summary>
		/// <param name="left">The first bytes to compare.</param>
		/// <param name="right">The second bytes to compare.</param>
		/// <returns>
		/// Whether both have the same length, contain only ASCII, and are equal when ASCII letters are compared ignoring case.
		/// </returns>
		public static bool EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
			Portable.AsciiEqualsIgnoreCase(left, right);
	}
}

namespace System.Text.Unicode
{
	/// <summary>The members of .NET's <c>System.Text.Unicode.Utf8</c> that the library uses.</summary>
	internal static class Utf8
	{
		/// <summary>
		/// Transcodes UTF-16 to UTF-8; stands in for <c>Utf8.FromUtf16</c>, with the same results. See
		/// <see cref="Portable.Utf16ToUtf8"/>.
		/// </summary>
		/// <param name="source">The UTF-16 text to transcode.</param>
		/// <param name="destination">The bytes to write the UTF-8 to.</param>
		/// <param name="charsRead">The number of chars of <paramref name="source"/> that were transcoded.</param>
		/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
		/// <param name="replaceInvalidSequences">
		/// Whether an invalid surrogate becomes U+FFFD; otherwise transcoding stops there with
		/// <see cref="OperationStatus.InvalidData"/>.
		/// </param>
		/// <param name="isFinalBlock">
		/// Whether <paramref name="source"/> is the end of the text; otherwise a high surrogate at its end waits for the
		/// low surrogate of the next block.
		/// </param>
		/// <returns>
		/// <see cref="OperationStatus.Done"/> when all of <paramref name="source"/> was transcoded, else the reason it
		/// stopped: <see cref="OperationStatus.DestinationTooSmall"/>, <see cref="OperationStatus.NeedMoreData"/> or
		/// <see cref="OperationStatus.InvalidData"/>.
		/// </returns>
		public static OperationStatus FromUtf16(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead,
			out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true) =>
			Portable.Utf16ToUtf8(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
	}
}
#endif
