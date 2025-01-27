// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Function that is used to determine if a field should get quoted when writing.
/// </summary>
public delegate bool ShouldQuote(ShouldQuoteArgs args);

/// <summary>
/// ShouldQuote args.
/// </summary>
public readonly struct ShouldQuoteArgs
{
	/// <summary>
	/// The field.
	/// </summary>
	public readonly string? Field;

	/// <summary>
	/// The field type.
	/// </summary>
	public readonly Type FieldType;

	/// <summary>
	/// The row.
	/// </summary>
	public readonly IWriterRow Row;

	/// <summary>
	/// Creates a new instance of ShouldQuoteArgs.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="fieldType">The field type.</param>
	/// <param name="row">The row.</param>
	public ShouldQuoteArgs(string? field, Type fieldType, IWriterRow row)
	{
		Field = field;
		FieldType = fieldType;
		Row = row;
	}
}
