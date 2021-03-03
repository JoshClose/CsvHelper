// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.TypeConversion;
using System.Collections.Generic;
using System.IO;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="IWriter"/>.
	/// </summary>
	public interface IWriterConfiguration
	{
		/// <summary>
		/// The mode.
		/// See <see cref="CsvMode"/> for more details.
		/// </summary>
		CsvMode Mode { get; }

		/// <summary>
		/// A value indicating whether to leave the <see cref="TextReader"/> or <see cref="TextWriter"/> open after this object is disposed.
		/// </summary>
		/// <value>
		///   <c>true</c> to leave open, otherwise <c>false</c>.
		/// </value>
		bool LeaveOpen { get; }

		/// <summary>
		/// Gets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		string Delimiter { get; }

		/// <summary>
		/// Gets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		char Quote { get; }

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
		/// Gets a value indicating if fields should be sanitized
		/// to prevent malicious injection. This covers MS Excel, 
		/// Google Sheets and Open Office Calc.
		/// </summary>
		bool SanitizeForInjection { get; }

		/// <summary>
		/// Gets the characters that are used for injection attacks.
		/// </summary>
		char[] InjectionCharacters { get; }

		/// <summary>
		/// Gets the character used to escape a detected injection.
		/// </summary>
		char InjectionEscapeCharacter { get; }

		/// <summary>
		/// The newline string to use. Default is <see cref="Environment.NewLine"/>.
		/// When writing, this value is always used.
		/// When reading, this value is only used if explicitly set. If not set,
		/// the parser uses one of \r\n, \r, or \n.
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
		/// Gets a function that is used to determine if a field should get quoted 
		/// when writing.
		/// </summary>
		ShouldQuote ShouldQuote { get; }

		/// <summary>
		/// Gets the culture info used to read and write CSV files.
		/// </summary>
		CultureInfo CultureInfo { get; }

		/// <summary>
		/// Gets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		bool AllowComments { get; }

		/// <summary>
		/// Gets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		char Comment { get; }

		/// <summary>
		/// Gets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		bool HasHeaderRecord { get; }

		/// <summary>
		/// Gets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		bool IgnoreReferences { get; }

		/// <summary>
		/// Gets a value indicating if private
		/// member should be read from and written to.
		/// True to include private member, otherwise false. Default is false.
		/// </summary>
		bool IncludePrivateMembers { get; }

		/// <summary>
		/// Gets a callback that will return the prefix for a reference header.
		/// </summary>
		ReferenceHeaderPrefix ReferenceHeaderPrefix { get; }

		/// <summary>
		/// Gets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		MemberTypes MemberTypes { get; }

		/// <summary>
		/// Gets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		bool UseNewObjectForNullReferenceMembers { get; }

		/// <summary>
		/// Gets the comparer used to order the properties
		/// of dynamic objects when writing. The default is null,
		/// which will preserve the order the object properties
		/// were created with.
		/// </summary>
		IComparer<string> DynamicPropertySort { get; }

		/// <summary>
		/// A value indicating if exception messages contain raw CSV data.
		/// <c>true</c> if exception contain raw CSV data, otherwise <c>false</c>.
		/// Default is <c>true</c>.
		/// </summary>
		bool ExceptionMessagesContainRawData { get; }
	}
}
