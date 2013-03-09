// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.ComponentModel;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to read parsed data
	/// from a CSV file row.
	/// </summary>
	public interface ICsvReaderRow
	{
		/// <summary>
		/// Get the current record;
		/// </summary>
		string[] CurrentRecord { get; }

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		string this[ int index ] { get; }

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		string this[ string name ] { get; }

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		string this[ string name, int index ] { get; }

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		string GetField( int index );

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		string GetField( string name );

		/// <summary>
		/// Gets the raw field at position (column) name and the index
		/// instance of that field. The index is used when there are
		/// multiple columns with the same header name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The raw field.</returns>
		string GetField( string name, int index );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( int index );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( string name );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns></returns>
		T GetField<T>( string name, int index );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( int index, ITypeConverter converter );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( string name, ITypeConverter converter );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T>( string name, int index, ITypeConverter converter );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T, TConverter>( int index ) where TConverter : ITypeConverter;

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T, TConverter>( string name ) where TConverter : ITypeConverter;

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		T GetField<T, TConverter>( string name, int index ) where TConverter : ITypeConverter;

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( int index, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, int index, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( int index, ITypeConverter converter, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, ITypeConverter converter, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T>( string name, int index, ITypeConverter converter, out T field );

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T, TConverter>( int index, out T field ) where TConverter : ITypeConverter;

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T, TConverter>( string name, out T field ) where TConverter : ITypeConverter;

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		bool TryGetField<T, TConverter>( string name, int index, out T field ) where TConverter : ITypeConverter;

		/// <summary>
		/// Determines whether the current record is empty.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <returns>
		///   <c>true</c> if [is record empty]; otherwise, <c>false</c>.
		/// </returns>
		bool IsRecordEmpty();

#if !NET_2_0
		/// <summary>
		/// Gets the record converted into <see cref="Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="Type"/> T.</returns>
		T GetRecord<T>() where T : class;

		/// <summary>
		/// Gets the record.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the record.</param>
		/// <returns>The record.</returns>
		object GetRecord( Type type );

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		IEnumerable<T> GetRecords<T>() where T : class;

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the record.</param>
		/// <returns>An <see cref="IList{Object}" /> of records.</returns>
		IEnumerable<object> GetRecords( Type type );

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		void InvalidateRecordCache<T>() where T : class;

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="ICsvReader.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReader.InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		/// <param name="type">The type to invalidate.</param>
		void InvalidateRecordCache( Type type );
#endif
	}
}
