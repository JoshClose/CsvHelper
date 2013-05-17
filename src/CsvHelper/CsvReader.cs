// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if NET_2_0
using CsvHelper.MissingFrom20;
#endif
#if !NET_2_0
using System.Linq;
using System.Linq.Expressions;
#endif
#if !NET_2_0 && !NET_3_5
using System.Dynamic;
#endif
#if WINRT_4_5
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="ICsvParser" />.
	/// </summary>
	public class CsvReader : ICsvReader
	{
		private bool disposed;
		private bool hasBeenRead;
		private string[] currentRecord;
		private string[] headerRecord;
		private ICsvParser parser;
		private int currentIndex = -1;
		private bool doneReading;
		private readonly Dictionary<string, List<int>> namedIndexes = new Dictionary<string, List<int>>();
#if !NET_2_0
		private readonly Dictionary<Type, Delegate> recordFuncs = new Dictionary<Type, Delegate>();
#endif
		private readonly CsvConfiguration configuration;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual CsvConfiguration Configuration
		{
			get { return configuration; }
		}

		/// <summary>
		/// Gets the parser.
		/// </summary>
		public virtual ICsvParser Parser
		{
			get { return parser; }
		}

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		public virtual string[] FieldHeaders
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return headerRecord;
			}
		}

		/// <summary>
		/// Get the current record;
		/// </summary>
		public virtual string[] CurrentRecord
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return currentRecord;
			}
		}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/> and
		/// <see cref="CsvParser"/> as the default parser.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public CsvReader( TextReader reader )
		{
			if( reader == null )
			{
				throw new ArgumentNullException( "reader" );
			}

			configuration = new CsvConfiguration();
			parser = new CsvParser( reader, configuration );
		}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/> and
		/// <see cref="CsvConfiguration"/> and <see cref="CsvParser"/> as the default parser.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvReader( TextReader reader, CsvConfiguration configuration )
		{
			if( reader == null )
			{
				throw new ArgumentNullException( "reader" );
			}
			if( configuration == null )
			{
				throw new ArgumentNullException( "configuration" );
			}

			parser = new CsvParser( reader, configuration );
			this.configuration = configuration;
		}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="ICsvParser" />.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> used to parse the CSV file.</param>
		public CsvReader( ICsvParser parser )
		{
			if( parser == null )
			{
				throw new ArgumentNullException( "parser" );
			}
			if( parser.Configuration == null )
			{
				throw new CsvConfigurationException( "The given parser has no configuration." );
			}

			this.parser = parser;
			configuration = parser.Configuration;
		}

		/// <summary>
		/// Advances the reader to the next record.
		/// If HasHeaderRecord is true (true by default), the first record of
		/// the CSV file will be automatically read in as the header record
		/// and the second record will be returned.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual bool Read()
		{
			CheckDisposed();

			if( doneReading )
			{
				const string message =
					"The reader has already exhausted all records. " +
					"If you would like to iterate the records more than " +
					"once, store the records in memory. i.e. Use " +
					"CsvReader.GetRecords<T>().ToList()";
				throw new CsvReaderException( message );
			}

			if( configuration.HasHeaderRecord && headerRecord == null )
			{
				headerRecord = parser.Read();
				ParseNamedIndexes();
			}

			do
			{
				currentRecord = parser.Read();
			} 
			while( configuration.SkipEmptyRecords && IsRecordEmpty( false ) );

			currentIndex = -1;
			hasBeenRead = true;

			if( currentRecord == null )
			{
				doneReading = true;
			}

			return currentRecord != null;
		}

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[int index]
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return GetField( index );
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[string name]
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return GetField( name );
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[string name, int index]
		{
			get 
			{ 
				CheckDisposed();
				CheckHasBeenRead();

				return GetField( name, index );
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			// Set the current index being used so we
			// have more information if an error occurs
			// when reading records.
			currentIndex = index;

			if( index >= currentRecord.Length )
			{
				if( configuration.IsStrictMode )
				{
					throw new CsvMissingFieldException( string.Format( "Field at index '{0}' does not exist.", index ) );
				}
				return default( string );
			}

			return currentRecord[index];
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( string name )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var index = GetFieldIndex( name );
			if( index < 0 )
			{
				return null;
			}
			return GetField( index );
		}

		/// <summary>
		/// Gets the raw field at position (column) name and the index
		/// instance of that field. The index is used when there are
		/// multiple columns with the same header name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( string name, int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index );
			if( fieldIndex < 0 )
			{
				return null;
			}
			return GetField( fieldIndex );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( int index, ITypeConverter converter )
		{
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = configuration.CultureInfo
			};
			return converter.ConvertFromString( typeConverterOptions, currentRecord[index] );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( string name, ITypeConverter converter )
		{
			var index = GetFieldIndex( name );
			return GetField( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( string name, int index, ITypeConverter converter )
		{
			var fieldIndex = GetFieldIndex( name, index );
			return GetField( fieldIndex, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( string name )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns></returns>
		public virtual T GetField<T>( string name, int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( name, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( int index, ITypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			if( index >= currentRecord.Length )
			{
				if( configuration.IsStrictMode )
				{
					throw new CsvMissingFieldException( string.Format( "Field at index '{0}' does not exist.", index ) );
				}
				return default( T );
			}
			
			return (T)GetField( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( string name, ITypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var index = GetFieldIndex( name );
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( string name, int index, ITypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index );
			return GetField<T>( fieldIndex, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T, TConverter>( int index ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T, TConverter>( string name ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( name, converter );
		}

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
		public virtual T GetField<T, TConverter>( string name, int index ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( name, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( name, converter, out field );
		}

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
		public virtual bool TryGetField<T>( string name, int index, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( name, index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, ITypeConverter converter, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			// DateTimeConverter.ConvertFrom will successfully convert
			// a white space string to a DateTime.MinValue instead of
			// returning null, so we need to handle this special case.
			if( converter is DateTimeConverter )
			{
				if( StringHelper.IsNullOrWhiteSpace( currentRecord[index] ) )
				{
					field = default( T );
					return false;
				}
			}

			// TypeConverter.IsValid() just wraps a
 			// ConvertFrom() call in a try/catch, so lets not
			// do it twice and just do it ourselves.
			try
			{
				field = (T)GetField( index, converter );
				return true;
			}
			catch
			{
				field = default( T );
				return false;
			}
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, ITypeConverter converter, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var index = GetFieldIndex( name );
			if( index == -1 )
			{
				field = default( T );
				return false;
			}
			return TryGetField( index, converter, out field );
		}

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
		public virtual bool TryGetField<T>( string name, int index, ITypeConverter converter, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index );
			if( fieldIndex == -1 )
			{
				field = default( T );
				return false;
			}
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>( int index, out T field ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>( string name, out T field ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField( name, converter, out field );
		}

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
		public virtual bool TryGetField<T, TConverter>( string name, int index, out T field ) where TConverter : ITypeConverter
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField( name, index, converter, out field );
		}

		/// <summary>
		/// Determines whether the current record is empty.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <returns>
		///   <c>true</c> if [is record empty]; otherwise, <c>false</c>.
		/// </returns>
		public virtual bool IsRecordEmpty()
		{
			return IsRecordEmpty( true );
		}

#if !NET_2_0
		/// <summary>
		/// Gets the record converted into <see cref="Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="Type"/> T.</returns>
		public virtual T GetRecord<T>() where T : class
		{
			CheckDisposed();
			CheckHasBeenRead();

			T record;
			try
			{
				record = CreateRecord<T>();
			}
			catch( Exception ex )
			{
				ExceptionHelper.AddExceptionDataMessage( ex, parser, typeof( T ), namedIndexes, currentIndex, currentRecord );
				throw;
			}
			return record;
		}

		/// <summary>
		/// Gets the record.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the record.</param>
		/// <returns>The record.</returns>
		public virtual object GetRecord( Type type )
		{
			CheckDisposed();
			CheckHasBeenRead();

			object record;
			try
			{
				record = CreateRecord( type );
			}
			catch( Exception ex )
			{
				ExceptionHelper.AddExceptionDataMessage( ex, parser, type, namedIndexes, currentIndex, currentRecord );
				throw;
			}
			return record;
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		public virtual IEnumerable<T> GetRecords<T>() where T : class
		{
			CheckDisposed();
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			while( Read() )
			{
				T record;
				try
				{
					record = CreateRecord<T>();
				}
				catch( Exception ex )
				{
					ExceptionHelper.AddExceptionDataMessage( ex, parser, typeof( T ), namedIndexes, currentIndex, currentRecord );
					throw;
				}

				yield return record;
			}
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the record.</param>
		/// <returns>An <see cref="IList{Object}" /> of records.</returns>
		public virtual IEnumerable<object> GetRecords( Type type )
		{
			CheckDisposed();
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			while( Read() )
			{
				object record;
				try
				{
					record = CreateRecord( type );
				}
				catch( Exception ex )
				{
					ExceptionHelper.AddExceptionDataMessage( ex, parser, type, namedIndexes, currentIndex, currentRecord );
					throw;
				}
				yield return record;
			}
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReaderRow.ClearRecordCache{T}"/> needs to be called to update the
		/// record cache.
		/// </summary>
		public virtual void ClearRecordCache<T>() where T : class
		{
			ClearRecordCache( typeof( T ) );
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReader.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReaderRow.ClearRecordCache(System.Type)"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <param name="type">The type to invalidate.</param>
		public virtual void ClearRecordCache( Type type )
		{
			recordFuncs.Remove( type );
		}

		/// <summary>
		/// Clears the record cache for all types. After <see cref="ICsvReader.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReaderRow.ClearRecordCache()"/> needs to be called to update the
		/// record cache.
		/// </summary>
		public virtual void ClearRecordCache()
		{
			recordFuncs.Clear();
		}
#endif

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose( bool disposing )
		{
			if( !disposed )
			{
				if( disposing )
				{
					if( parser != null )
					{
						parser.Dispose();
					}
				}

				disposed = true;
				parser = null;
			}
		}

		/// <summary>
		/// Checks if the instance has been disposed of.
		/// </summary>
		/// <exception cref="ObjectDisposedException" />
		protected virtual void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}

		/// <summary>
		/// Checks if the reader has been read yet.
		/// </summary>
		/// <exception cref="CsvReaderException" />
		protected virtual void CheckHasBeenRead()
		{
			if( !hasBeenRead )
			{
				throw new CsvReaderException( "You must call read on the reader before accessing its data." );
			}
		}

		/// <summary>
		/// Determines whether the current record is empty.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <param name="checkHasBeenRead">True to check if the record 
		/// has been read, otherwise false.</param>
		/// <returns>
		///   <c>true</c> if [is record empty]; otherwise, <c>false</c>.
		/// </returns>
		protected virtual bool IsRecordEmpty( bool checkHasBeenRead )
		{
			CheckDisposed();
			if( checkHasBeenRead )
			{
				CheckHasBeenRead();
			}

			if( currentRecord == null )
			{
				return false;
			}

#if NET_2_0
			return EnumerableHelper.All( currentRecord, string.IsNullOrEmpty );
#else
			return currentRecord.All( string.IsNullOrEmpty );
#endif
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="name">The name of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="CsvReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="CsvMissingFieldException">Thrown if there isn't a field with name.</exception>
		protected virtual int GetFieldIndex( string name, int index = 0 )
		{
			return GetFieldIndex( new[] { name }, index );
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="names">The possible names of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="CsvReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="CsvMissingFieldException">Thrown if there isn't a field with name.</exception>
		protected virtual int GetFieldIndex( string[] names, int index = 0 )
		{
			if( names == null )
			{
				throw new ArgumentNullException( "names" );
			}

			if( !configuration.HasHeaderRecord )
			{
				throw new CsvReaderException( "There is no header record to determine the index by name." );
			}

			if( !Configuration.IsCaseSensitive )
			{
				for( var i = 0; i < names.Length; i++ )
				{
					names[i] = names[i].ToLower();
				}
			}

			var compareOptions = !Configuration.IsCaseSensitive ? CompareOptions.IgnoreCase : CompareOptions.None;
#if !NET_2_0
			var name =
				( from i in namedIndexes
				  from n in names
				  where Configuration.CultureInfo.CompareInfo.Compare( i.Key, n, compareOptions ) == 0
				  select i.Key ).SingleOrDefault();
#else
			string name = null;
			foreach( var pair in namedIndexes )
			{
				foreach( var n in names )
				{
					if( Configuration.CultureInfo.CompareInfo.Compare( pair.Key, n, compareOptions ) == 0 )
					{
						name = pair.Key;
					}
				}
			}
#endif
			if( name == null )
			{
				if( configuration.IsStrictMode )
				{
					// If we're in strict reading mode and the
					// named index isn't found, throw an exception.
					var namesJoined = string.Format( "'{0}'", string.Join( "', '", names ) );
					throw new CsvMissingFieldException( string.Format( "Fields {0} do not exist in the CSV file.", namesJoined ) );
				}
				return -1;
			}

			return namedIndexes[name][index];
		}

		/// <summary>
		/// Parses the named indexes from the header record.
		/// </summary>
		protected virtual void ParseNamedIndexes()
		{
			if( headerRecord == null )
			{
				throw new CsvReaderException( "No header record was found." );
			}

			for( var i = 0; i < headerRecord.Length; i++ )
			{
				var name = headerRecord[i];
				if( !Configuration.IsCaseSensitive )
				{
					name = name.ToLower();
				}

				if( namedIndexes.ContainsKey( name ) )
				{
					namedIndexes[name].Add( i );
				}
				else
				{
					namedIndexes[name] = new List<int> { i };
				}
			}
		}

#if !NET_2_0
		/// <summary>
		/// Creates the record for the given type.
		/// </summary>
		/// <typeparam name="T">The type of record to create.</typeparam>
		/// <returns>The created record.</returns>
		protected virtual T CreateRecord<T>() where T : class
		{
#if !NET_3_5
			// If the type is an object, a dynamic
			// object will be created. That is the
			// only way we can dynamically add properties
			// to a type of object.
			if( typeof( T ) == typeof( object ) )
			{
				return CreateDynamic();
			}
#endif

			return GetReadRecordFunc<T>()();
		}

		/// <summary>
		/// Creates the record for the given type.
		/// </summary>
		/// <param name="type">The type of record to create.</param>
		/// <returns>The created record.</returns>
		protected virtual object CreateRecord( Type type )
		{
#if !NET_3_5
			// If the type is an object, a dynamic
			// object will be created. That is the
			// only way we can dynamically add properties
			// to a type of object.
			if( type == typeof( object ) )
			{
				return CreateDynamic();
			}
#endif

			return GetReadRecordFunc( type ).DynamicInvoke();
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of object that is created
		/// and populated.</typeparam>
		/// <returns>The function delegate.</returns>
		protected virtual Func<T> GetReadRecordFunc<T>() where T : class
		{
			var recordType = typeof( T );
			CreateReadRecordFunc( recordType );

			return (Func<T>)recordFuncs[recordType];
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <param name="recordType">The <see cref="Type"/> of object that is created
		/// and populated.</param>
		/// <returns>The function delegate.</returns>
		protected virtual Delegate GetReadRecordFunc( Type recordType )
		{
			CreateReadRecordFunc( recordType );

			return recordFuncs[recordType];
		}

		/// <summary>
		/// Creates the read record func for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="recordType">Type of the record.</param>
		/// <param name="expressionCompiler">The expression compiler.</param>
		protected virtual void CreateReadRecordFunc( Type recordType )
		{
			if( recordFuncs.ContainsKey( recordType ) )
			{
				return;
			}

			var bindings = new List<MemberBinding>();

			if( configuration.Maps[recordType] == null )
			{
				configuration.Maps.Add( configuration.AutoMap( recordType ) );
			}

			CreatePropertyBindingsForMapping( configuration.Maps[recordType], recordType, bindings );

			var constructorExpression = configuration.Maps[recordType].Constructor ?? Expression.New( recordType );
			var body = Expression.MemberInit( constructorExpression, bindings );
			var funcType = typeof( Func<> ).MakeGenericType( recordType );
			recordFuncs[recordType] = Expression.Lambda( funcType, body ).Compile();
		}

		/// <summary>
		/// Creates the property bindings for the given <see cref="CsvClassMap"/>.
		/// </summary>
		/// <param name="mapping">The mapping to create the bindings for.</param>
		/// <param name="recordType">The type of record.</param>
		/// <param name="bindings">The bindings that will be added to from the mapping.</param>
		protected virtual void CreatePropertyBindingsForMapping( CsvClassMap mapping, Type recordType, List<MemberBinding> bindings )
		{
			AddPropertyBindings( mapping.PropertyMaps, bindings );

			foreach( var referenceMap in mapping.ReferenceMaps )
			{
				if( !CanRead( referenceMap ) )
				{
					continue;
				}

				var referenceBindings = new List<MemberBinding>();
				CreatePropertyBindingsForMapping( referenceMap.Mapping, referenceMap.Property.PropertyType, referenceBindings );
				var referenceBody = Expression.MemberInit( Expression.New( referenceMap.Property.PropertyType ), referenceBindings );
				bindings.Add( Expression.Bind( referenceMap.Property, referenceBody ) );
			}
		}

		/// <summary>
		/// Adds a <see cref="MemberBinding"/> for each property for it's field.
		/// </summary>
		/// <param name="properties">The properties to add bindings for.</param>
		/// <param name="bindings">The bindings that will be added to from the properties.</param>
		protected virtual void AddPropertyBindings( CsvPropertyMapCollection properties, List<MemberBinding> bindings )
		{
			foreach( var propertyMap in properties )
			{
				if( propertyMap.Data.ConvertExpression != null )
				{
					// The user is providing the expression to do the conversion.
					var exp = Expression.Invoke( propertyMap.Data.ConvertExpression, Expression.Constant( this ) );
					bindings.Add( Expression.Bind( propertyMap.Data.Property, exp ) );
					continue;
				}

				if( !CanRead( propertyMap ) )
				{
					continue;
				}

				if( propertyMap.Data.TypeConverter == null || !propertyMap.Data.TypeConverter.CanConvertFrom( typeof( string ) ) )
				{
					// Skip if the type isn't convertible.
					continue;
				}

				var index = propertyMap.Data.Index < 0 ? GetFieldIndex( propertyMap.Data.Names.ToArray() ) : propertyMap.Data.Index;
				if( index == -1 )
				{
					// Skip if the index was not found.
					continue;
				}

				// Get the field using the field index.
				var method = typeof( ICsvReaderRow ).GetProperty( "Item", typeof( string ), new[] { typeof( int ) } ).GetGetMethod();
				Expression fieldExpression = Expression.Call( Expression.Constant( this ), method, Expression.Constant( index, typeof( int ) ) );

				// Convert the field.
				var typeConverterExpression = Expression.Constant( propertyMap.Data.TypeConverter );
				if( propertyMap.Data.TypeConverterOptions.CultureInfo == null )
				{
					propertyMap.Data.TypeConverterOptions.CultureInfo = configuration.CultureInfo;
				}
				var typeConverterOptions = Expression.Constant( propertyMap.Data.TypeConverterOptions );

				// Create type converter expression.
				Expression typeConverterFieldExpression = Expression.Call( typeConverterExpression, "ConvertFromString", null, typeConverterOptions, fieldExpression );
				typeConverterFieldExpression = Expression.Convert( typeConverterFieldExpression, propertyMap.Data.Property.PropertyType );

				if( propertyMap.Data.IsDefaultSet )
				{
					// Create default value expression.
					Expression defaultValueExpression = Expression.Convert( Expression.Constant( propertyMap.Data.Default ), propertyMap.Data.Property.PropertyType );

					var checkFieldEmptyExpression = Expression.Equal( Expression.Convert( fieldExpression, typeof( string ) ), Expression.Constant( string.Empty, typeof( string ) ) );
					fieldExpression = Expression.Condition( checkFieldEmptyExpression, defaultValueExpression, typeConverterFieldExpression );
				}
				else
				{
					fieldExpression = typeConverterFieldExpression;
				}

				bindings.Add( Expression.Bind( propertyMap.Data.Property, fieldExpression ) );
			}
		}

		/// <summary>
		/// Determines if the property for the <see cref="CsvPropertyMap"/>
		/// can be read.
		/// </summary>
		/// <param name="propertyMap">The property map.</param>
		/// <returns>A value indicating of the property can be read. True if it can, otherwise false.</returns>
		protected bool CanRead( CsvPropertyMap propertyMap )
		{
			var cantRead =
				// Ignored properties.
				propertyMap.Data.Ignore ||
				// Properties that don't have a public setter
				// and we are honoring the accessor modifier.
				propertyMap.Data.Property.GetSetMethod() == null && !configuration.IgnorePrivateAccessor ||
				// Properties that don't have a setter at all.
				propertyMap.Data.Property.GetSetMethod( true ) == null;
			return !cantRead;
		}

		/// <summary>
		/// Determines if the property for the <see cref="CsvPropertyReferenceMap"/>
		/// can be read.
		/// </summary>
		/// <param name="propertyReferenceMap">The reference map.</param>
		/// <returns>A value indicating of the property can be read. True if it can, otherwise false.</returns>
		protected bool CanRead( CsvPropertyReferenceMap propertyReferenceMap )
		{
			var cantRead =
				// Properties that don't have a public setter
				// and we are honoring the accessor modifier.
				propertyReferenceMap.Property.GetSetMethod() == null && !configuration.IgnorePrivateAccessor ||
				// Properties that don't have a setter at all.
				propertyReferenceMap.Property.GetSetMethod( true ) == null;
			return !cantRead;
		}
#endif

#if !NET_2_0 && !NET_3_5
		/// <summary>
		/// Creates a dynamic object from the current record.
		/// </summary>
		/// <returns>The dynamic object.</returns>
		protected dynamic CreateDynamic()
		{
			var obj = new ExpandoObject();
			var dict = obj as IDictionary<string, object>;
			if( headerRecord != null )
			{
				for( var i = 0; i < headerRecord.Length; i++ )
				{
					var header = headerRecord[i];
					var field = currentRecord[i];
					dict.Add( header, field );
				}
			}
			else
			{
				for( var i = 0; i < currentRecord.Length; i++ )
				{
					var propertyName = "Field" + ( i + 1 );
					var field = currentRecord[i];
					dict.Add( propertyName, field );
				}
			}

			return obj;
		}
#endif
	}
}
