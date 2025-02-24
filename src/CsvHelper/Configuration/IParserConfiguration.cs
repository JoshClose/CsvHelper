﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Delegates;
using System.Globalization;
using System.Text;

namespace CsvHelper.Configuration;

/// <summary>
/// Configuration used for the <see cref="IParser"/>.
/// </summary>
public interface IParserConfiguration
{
	/// <summary>
	/// Gets the culture info used to read an write CSV files.
	/// </summary>
	CultureInfo CultureInfo { get; }

	/// <summary>
	/// Cache fields that are created when parsing.
	/// Default is false.
	/// </summary>
	bool CacheFields { get; }

	/// <summary>
	/// The newline string to use. Default is \r\n (CRLF).
	/// When writing, this value is always used.
	/// When reading, this value is only used if explicitly set.
	/// If not set, the parser uses one of \r\n, \r, or \n.
	/// </summary>
	string NewLine { get; }

	/// <summary>
	/// A value indicating if <see cref="NewLine"/> was set.
	/// </summary>
	/// <value>
	///   <c>true</c> if <see cref="NewLine"/> was set. <c>false</c> if <see cref="NewLine"/> is the default.
	/// </value>
	bool IsNewLineSet { get; }

	/// <summary>
	/// The mode.
	/// See <see cref="CsvMode"/> for more details.
	/// </summary>
	CsvMode Mode { get; }

	/// <summary>
	/// Gets the size of the buffer
	/// used for parsing and writing CSV files.
	/// Default is 0x1000.
	/// </summary>
	int BufferSize { get; }

	/// <summary>
	/// The size of the buffer used when processing fields.
	/// Default is 1024.
	/// </summary>
	int ProcessFieldBufferSize { get; }

	/// <summary>
	/// Gets a value indicating whether the number of bytes should
	/// be counted while parsing. Default is false. This will slow down parsing
	/// because it needs to get the byte count of every char for the given encoding.
	/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
	/// </summary>
	bool CountBytes { get; }

	/// <summary>
	/// Gets the encoding used when counting bytes.
	/// </summary>
	Encoding Encoding { get; }

	/// <summary>
	/// Gets the function that is called when bad field data is found. A field
	/// has bad data if it contains a quote and the field is not quoted (escaped).
	/// You can supply your own function to do other things like logging the issue
	/// instead of throwing an exception.
	/// </summary>
	BadDataFound? BadDataFound { get; }

	/// <summary>
	/// Gets or sets the maximum size of a field.
	/// Defaults to 0, indicating maximum field size is not checked.
	/// </summary>
	double MaxFieldSize { get; }

	/// <summary>
	/// Gets a value indicating if a line break found in a quote field should
	/// be considered bad data. <c>true</c> to consider a line break bad data, otherwise <c>false</c>.
	/// Defaults to false.
	/// </summary>
	bool LineBreakInQuotedFieldIsBadData { get; }

	/// <summary>
	/// Gets the character used to denote
	/// a line that is commented out. Default is '#'.
	/// </summary>
	char Comment { get; }

	/// <summary>
	/// Gets a value indicating if comments are allowed.
	/// <c>true</c> to allow commented out lines, otherwise <c>false</c>.
	/// </summary>
	bool AllowComments { get; }

	/// <summary>
	/// Gets a value indicating if blank lines
	/// should be ignored when reading.
	/// <c>true</c> to ignore, otherwise <c>false</c>. Default is true.
	/// </summary>
	bool IgnoreBlankLines { get; }

	/// <summary>
	/// Gets the character used to quote fields.
	/// Default is '"'.
	/// </summary>
	char Quote { get; }

	/// <summary>
	/// The delimiter used to separate fields.
	/// Default is <see cref="TextInfo.ListSeparator"/>.
	/// </summary>
	string Delimiter { get; }

	/// <summary>
	/// Detect the delimiter instead of using the delimiter from configuration.
	/// Default is <c>false</c>.
	/// </summary>
	bool DetectDelimiter { get; }

	/// <summary>
	/// Gets the function that is called when <see cref="DetectDelimiter"/> is enabled.
	/// </summary>
	GetDelimiter GetDelimiter { get; }

	/// <summary>
	/// The possible delimiter values used when detecting the delimiter.
	/// Default is [",", ";", "|", "\t"].
	/// </summary>
	string[] DetectDelimiterValues { get; }

	/// <summary>
	/// The character used to escape characters.
	/// Default is '"'.
	/// </summary>
	char Escape { get; }

	/// <summary>
	/// Gets the field trimming options.
	/// </summary>
	TrimOptions TrimOptions { get; }

	/// <summary>
	/// Characters considered whitespace.
	/// Used when trimming fields.
	/// Default is [' '].
	/// </summary>
	char[] WhiteSpaceChars { get; }

	/// <summary>
	/// A value indicating if exception messages contain raw CSV data.
	/// <c>true</c> if exception contain raw CSV data, otherwise <c>false</c>.
	/// Default is <c>true</c>.
	/// </summary>
	bool ExceptionMessagesContainRawData { get; }

	/// <summary>
	/// Validates the configuration.
	/// </summary>
	void Validate();
}
