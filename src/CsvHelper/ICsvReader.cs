#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to read parsed data
	/// from a CSV file.
	/// </summary>
	public interface ICsvReader : IDisposable
	{
		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Gets the parser.
		/// </summary>
		ICsvParser Parser { get; }

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		string[] FieldHeaders { get; }

		/// <summary>
		/// Advances the reader to the next record.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		bool Read();

		/// <summary>
		/// Gets the raw field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <returns>The raw field.</returns>
		string this[int index] { get; }

		/// <summary>
		/// Gets the raw string field at name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		string this[string name] { get; }

		/// <summary>
		/// Gets the raw field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <returns>The raw field.</returns>
		string GetField( int index );

		/// <summary>
		/// Gets the raw field at name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		string GetField( string name );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( int index );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( string name );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( int index, TypeConverter converter );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( string name, TypeConverter converter );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( int index, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index
		/// using the specified <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( int index, TypeConverter converter, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name
		/// using the specified <see cref="TypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, TypeConverter converter, out T field );

		/// <summary>
		/// Gets the record converted into <see cref="Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="Type"/> T.</returns>
		T GetRecord<T>() where T : class;

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		IEnumerable<T> GetRecords<T>() where T : class;

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		void InvalidateRecordCache<T>() where T : class;
	}
}
