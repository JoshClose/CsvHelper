// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// Mode to use when parsing and writing.
/// </summary>
public enum CsvMode
{
	/// <summary>
	/// Uses RFC 4180 format (default).
	/// If a field contains a <see cref="CsvConfiguration.Delimiter"/> or <see cref="CsvConfiguration.NewLine"/>,
	/// it is wrapped in <see cref="CsvConfiguration.Quote"/>s.
	/// If quoted field contains a <see cref="CsvConfiguration.Quote"/>, it is preceded by <see cref="CsvConfiguration.Escape"/>.
	/// </summary>
	RFC4180 = 0,

	/// <summary>
	/// Uses escapes.
	/// If a field contains a <see cref="CsvConfiguration.Delimiter"/>, <see cref="CsvConfiguration.NewLine"/>,
	/// or <see cref="CsvConfiguration.Escape"/>, it is preceded by <see cref="CsvConfiguration.Escape"/>.
	/// Newline defaults to \n.
	/// </summary>
	Escape,

	/// <summary>
	/// Doesn't use quotes or escapes.
	/// This will ignore quoting and escape characters. This means a field cannot contain a
	/// <see cref="CsvConfiguration.Delimiter"/>, <see cref="CsvConfiguration.Quote"/>, or
	/// <see cref="CsvConfiguration.NewLine"/>, as they cannot be escaped.
	/// </summary>
	NoEscape
}
