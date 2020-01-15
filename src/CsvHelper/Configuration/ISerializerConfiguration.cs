// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="ISerializer"/>.
	/// </summary>
	public interface ISerializerConfiguration
	{
		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		string Delimiter { get; set; }

		/// <summary>
		/// Gets or sets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		char Quote { get; set; }

		/// <summary>
		/// Gets or sets the escape character used to escape a quote inside a field.
		/// Default is '"'.
		/// </summary>
		char Escape { get; set; }

		/// <summary>
		/// Gets or sets the field trimming options.
		/// </summary>
		TrimOptions TrimOptions { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if fields should be sanitized
		/// to prevent malicious injection. This covers MS Excel, 
		/// Google Sheets and Open Office Calc.
		/// </summary>
		bool SanitizeForInjection { get; set; }

		/// <summary>
		/// Gets or sets the characters that are used for injection attacks.
		/// </summary>
		char[] InjectionCharacters { get; set; }

		/// <summary>
		/// Gets or sets the character used to escape a detected injection.
		/// </summary>
		char InjectionEscapeCharacter { get; set; }

		/// <summary>
		/// Gets or sets the newline to use when writing.
		/// </summary>
		NewLine NewLine { get; set; }

		/// <summary>
		/// Gets the newline string to use when writing. This string is determined
		/// by the <see cref="NewLine"/> value.
		/// </summary>
		string NewLineString { get; }
	}
}
