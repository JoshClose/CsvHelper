// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.IO;

namespace CsvHelper
{
	/// <summary>
	/// Defines context information used by the <see cref="CsvFieldReader"/>.
	/// </summary>
    public interface IFieldReaderContext
    {
		/// <summary>
		/// Gets the buffer used to store data from the <see cref="Reader"/>.
		/// </summary>
		char[] Buffer { get; }

		/// <summary>
		/// Gets the buffer position.
		/// </summary>
		int BufferPosition { get; }

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		long BytePosition { get; }

		/// <summary>
		/// Gets the character position.
		/// </summary>
		long CharPosition { get; }

		/// <summary>
		/// Gets the number of characters read from the <see cref="Reader"/>.
		/// </summary>
		int CharsRead { get; }

		/// <summary>
		/// Gets the field.
		/// </summary>
		string Field { get; }

		/// <summary>
		/// Gets the field start position.
		/// </summary>
		int FieldStartPosition { get; }

		/// <summary>
		/// Gets the field end position.
		/// </summary>
		int FieldEndPosition { get; }

		/// <summary>
		/// Getsa value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// </summary>
		bool IsFieldBad { get; }

		/// <summary>
		/// Gets all the characters of the record including
		/// quotes, delimeters, and line endings.
		/// </summary>
		string RawRecord { get; }

		/// <summary>
		/// Gets the raw record start position.
		/// </summary>
		int RawRecordStartPosition { get; }

		/// <summary>
		/// Gets the raw record end position.
		/// </summary>
		int RawRecordEndPosition { get; }

		/// <summary>
		/// Gets a value indicating if the <see cref="TextReader"/>
		/// should be left open when disposing.
		/// </summary>
		bool LeaveOpen { get; }
	}
}
