// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to read a field in a CSV file.
	/// </summary>
	public interface IFieldReader : IDisposable
	{
		/// <summary>
		/// Gets the reading context.
		/// </summary>
		ReadingContext Context { get; }

		/// <summary>
		/// Gets a value indicating if the buffer is empty.
		/// True if the buffer is empty, otherwise false.
		/// </summary>
		bool IsBufferEmpty { get; }

		/// <summary>
		/// Fills the buffer.
		/// </summary>
		/// <returns>True if there is more data left.
		/// False if all the data has been read.</returns>
		bool FillBuffer();

		/// <summary>
		/// Fills the buffer asynchronously.
		/// </summary>
		/// <returns>True if there is more data left.
		/// False if all the data has been read.</returns>
		Task<bool> FillBufferAsync();

		/// <summary>
		/// Gets the next char as an <see cref="int"/>.
		/// </summary>
		int GetChar();

		/// <summary>
		/// Gets the field. This will append any reading progress.
		/// </summary>
		/// <returns>The current field.</returns>
		string GetField();

		/// <summary>
		/// Appends the current reading progress.
		/// </summary>
		void AppendField();

		/// <summary>
		/// Move's the buffer position according to the given offset.
		/// </summary>
		/// <param name="offset">The offset to move the buffer.</param>
		void SetBufferPosition(int offset = 0);

		/// <summary>
		/// Sets the start of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		void SetFieldStart(int offset = 0);

		/// <summary>
		/// Sets the end of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		void SetFieldEnd(int offset = 0);

		/// <summary>
		/// Sets the raw record start to the current buffer position;
		/// </summary>
		/// <param name="offset">An offset for the raw record start.
		/// The offset should be less than 1.</param>
		void SetRawRecordStart(int offset);

		/// <summary>
		/// Sets the raw record end to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the raw record end.
		/// The offset should be less than 1.</param>
		void SetRawRecordEnd(int offset);
	}
}
