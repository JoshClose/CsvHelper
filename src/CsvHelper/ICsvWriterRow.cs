using System;
using System.Collections.Generic;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to write a CSV row.
	/// </summary>
    public interface ICsvWriterRow
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		ICsvWriterConfiguration Configuration { get; }

		/// <summary>
		/// Gets the current row.
		/// </summary>
		int Row { get; }

		/// <summary>
		/// Get the current record;
		/// </summary>
		List<string> CurrentRecord { get; }

		/// <summary>
		/// Writes a field that has already been converted to a
		/// <see cref="string"/> from an <see cref="ITypeConverter"/>.
		/// If the field is null, it won't get written. A type converter 
		/// will always return a string, even if field is null. If the 
		/// converter returns a null, it means that the converter has already
		/// written data, and the returned value should not be written.
		/// </summary>
		/// <param name="field">The converted field to write.</param>
		void WriteConvertedField( string field );

		/// <summary>
		/// Writes the field to the CSV file. The field
		/// may get quotes added to it.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		void WriteField( string field );

		/// <summary>
		/// Writes the field to the CSV file. This will
		/// ignore any need to quote and ignore the
		/// <see cref="CsvConfiguration.QuoteAllFields"/>
		/// and just quote based on the shouldQuote
		/// parameter.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		/// <param name="shouldQuote">True to quote the field, otherwise false.</param>
		void WriteField( string field, bool shouldQuote );

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		void WriteField<T>( T field );

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		/// <param name="converter">The converter used to convert the field into a string.</param>
		void WriteField<T>( T field, ITypeConverter converter );

		/// <summary>
		/// Writes the field to the CSV file
		/// using the given <see cref="ITypeConverter"/>.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord()" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <typeparam name="TConverter">The type of the converter.</typeparam>
		/// <param name="field">The field to write.</param>
		void WriteField<T, TConverter>( T field );

        /// <summary>
        /// Write the Excel seperator record.
        /// </summary>
        void WriteExcelSeparator();

        /// <summary>
        /// Writes a comment.
        /// </summary>
        /// <param name="comment">The comment to write.</param>
        void WriteComment( string comment );

#if !NET_2_0

        /// <summary>
        /// Writes the header record from the given properties/fields.
        /// </summary>
        /// <typeparam name="T">The type of the record.</typeparam>
        void WriteHeader<T>();

		/// <summary>
		/// Writes the header record from the given properties/fields.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		void WriteHeader( Type type );

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		void WriteRecord<T>( T record );
		
#endif
	}
}
