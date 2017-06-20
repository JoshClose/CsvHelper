using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		IFieldReaderContext Context { get; }

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
		/// Sets the start of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		void SetFieldStart( int offset = 0 );

		/// <summary>
		/// Sets the end of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		void SetFieldEnd( int offset = 0 );

		/// <summary>
		/// Sets the raw record end to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the raw record end.
		/// The offset should be less than 1.</param>
		void SetRawRecordEnd( int offset );
	}
}
