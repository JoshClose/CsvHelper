using System;
using System.IO;

namespace CsvHelper.Excel;

/// <summary>
/// The exceptions for the XML of a part that can't be read: an <see cref="InvalidDataException"/> whose message names the
/// part and the byte offset, and whose <see cref="Exception.Data"/> keeps the offset and what is wrong, so that a sheet's
/// error can name its cell instead. (<see cref="InvalidDataException"/> is sealed, so it can't carry them as properties.)
/// </summary>
internal static class XmlError
{
	/// <summary>The key of the offset in <see cref="Exception.Data"/>.</summary>
	private const string OffsetKey = "CsvHelper.Excel.XmlOffset";

	/// <summary>The key of what is wrong in <see cref="Exception.Data"/>.</summary>
	private const string DetailKey = "CsvHelper.Excel.XmlDetail";

	/// <summary>Creates the exception.</summary>
	/// <param name="partName">The name of the part, such as <c>xl/worksheets/sheet1.xml</c>.</param>
	/// <param name="offset">The offset in the part of what can't be read.</param>
	/// <param name="detail">What is wrong, as a clause, such as "it ends inside a tag".</param>
	/// <returns>The exception, to throw.</returns>
	public static InvalidDataException Create(string partName, long offset, string detail)
	{
		var exception = new InvalidDataException($"The XML of {partName} can't be read at byte {offset}: {detail}.");
		exception.Data[OffsetKey] = offset;
		exception.Data[DetailKey] = detail;

		return exception;
	}

	/// <summary>Gets the offset and what is wrong from an exception that <see cref="Create"/> made.</summary>
	/// <param name="exception">The exception.</param>
	/// <param name="offset">The offset in the part of what can't be read.</param>
	/// <param name="detail">What is wrong, as a clause.</param>
	/// <returns>Whether the exception is one of the XML's; <see langword="false"/> for any other, such as a limit's.</returns>
	public static bool TryGet(Exception exception, out long offset, out string detail)
	{
		if (exception.Data[OffsetKey] is long value && exception.Data[DetailKey] is string text)
		{
			offset = value;
			detail = text;

			return true;
		}

		offset = 0;
		detail = "";

		return false;
	}
}
