using System;

namespace CsvHelper;

/// <summary>
/// Thrown when the method of a <see cref="CsvValidateAttribute"/> rejects the value of a member. The record methods of
/// <see cref="CsvReader"/> report it as the <see cref="Exception.InnerException"/> of a <see cref="CsvFormatException"/>
/// that names the record, the field and the member.
/// </summary>
public sealed class CsvValidationException : FormatException
{
	/// <summary>Creates the exception for a value that is not valid.</summary>
	/// <param name="message">The message that says what is wrong.</param>
	public CsvValidationException(string message) : base(message) { }
}
