﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Represents errors that occur due to bad data.
/// </summary>
[Serializable]
public class BadDataException : CsvHelperException
{
	/// <summary>
	/// The full field unedited.
	/// </summary>
	public readonly string Field;

	/// <summary>
	/// The full row unedited.
	/// </summary>
	public readonly string RawRecord;

	/// <summary>
	/// Initializes a new instance of the <see cref="BadDataException"/> class.
	/// </summary>
	/// <param name="field">The full field unedited.</param>
	/// <param name="rawRecord">The full row unedited.</param>
	/// <param name="context">The reading context.</param>
	public BadDataException(string field, string rawRecord, CsvContext context) : base(context)
	{
		Field = field;
		RawRecord = rawRecord;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BadDataException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="field">The full field unedited.</param>
	/// <param name="rawRecord">The full row unedited.</param>
	/// <param name="context">The reading context.</param>
	/// <param name="message">The message that describes the error.</param>
	public BadDataException(string field, string rawRecord, CsvContext context, string message) : base(context, message)
	{
		Field = field;
		RawRecord = rawRecord;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BadDataException"/> class
	/// with a specified error message and a reference to the inner exception that 
	/// is the cause of this exception.
	/// </summary>
	/// <param name="field">The full field unedited.</param>
	/// <param name="rawRecord">The full row unedited.</param>
	/// <param name="context">The reading context.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
	public BadDataException(string field, string rawRecord, CsvContext context, string message, Exception innerException) : base(context, message, innerException)
	{
		Field = field;
		RawRecord = rawRecord;
	}
}
