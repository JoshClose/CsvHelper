// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if !NET_2_0
using System.Linq;
using System.Linq.Expressions;
#endif
#if NET_2_0
using CsvHelper.MissingFrom20;
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
			var culture = Configuration.UseInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
			return converter.ConvertFromString( culture, currentRecord[index] );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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
#if NET_2_0
				if( StringHelper.IsNullOrWhiteSpace( currentRecord[index] ) )
#elif NET_3_5
				if( currentRecord[index].IsNullOrWhiteSpace() )
#else
				if( string.IsNullOrWhiteSpace( currentRecord[index] ) )
#endif
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
				record = GetReadRecordFunc<T>()( this );
			}
			catch( CsvReaderException )
			{
				// We threw the exception, so let it go.
				throw;
			}
			catch( Exception ex )
			{
				throw ExceptionHelper.GetReaderException<CsvReaderException>( "An error occurred reading the record.", ex, parser, typeof( T ), namedIndexes, currentIndex, currentRecord );
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
				record = GetReadRecordFunc( type )( this );
			}
			catch( CsvReaderException )
			{
				// We threw the exception, so let it go.
				throw;
			}
			catch( Exception ex )
			{
				throw ExceptionHelper.GetReaderException<CsvReaderException>( "An error occurred reading the record.", ex, parser, type, namedIndexes, currentIndex, currentRecord );
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
					record = GetReadRecordFunc<T>()( this );
				}
				catch( CsvReaderException )
				{
					// We threw the exception, so let it go.
					throw;
				}
				catch( Exception ex )
				{
					throw ExceptionHelper.GetReaderException<CsvReaderException>( "An error occurred reading the record.", ex, parser, typeof( T ), namedIndexes, currentIndex, currentRecord );
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
					record = GetReadRecordFunc( type )( this );
				}
				catch( CsvReaderException )
				{
					// We threw the exception, so let it go.
					throw;
				}
				catch( Exception ex )
				{
					throw ExceptionHelper.GetReaderException<CsvReaderException>( "An error occurred reading the record.", ex, parser, type, namedIndexes, currentIndex, currentRecord );
				}
				yield return record;
			}
		}

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="ICsvReader.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReader.InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		public virtual void InvalidateRecordCache<T>() where T : class
		{
			recordFuncs.Remove( typeof( T ) );
			configuration.Properties.Clear();
		}

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="ICsvReader.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvReader.InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		/// <param name="type">The type to invalidate.</param>
		public virtual void InvalidateRecordCache( Type type )
		{
			recordFuncs.Remove( type );
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

			CultureInfo culture;
			CompareOptions compareOptions;
			if( Configuration.UseInvariantCulture && !Configuration.IsCaseSensitive )
			{
				culture = CultureInfo.InvariantCulture;
				compareOptions = CompareOptions.IgnoreCase;
			}
			else if( Configuration.UseInvariantCulture && Configuration.IsCaseSensitive )
			{
				culture = CultureInfo.InvariantCulture;
				compareOptions = CompareOptions.None;
			}
			else if( !Configuration.UseInvariantCulture && !Configuration.IsCaseSensitive )
			{
				culture = CultureInfo.CurrentCulture;
				compareOptions = CompareOptions.IgnoreCase;
			}
			else
			{
				culture = CultureInfo.CurrentCulture;
				compareOptions = CompareOptions.None;
			}
#if !NET_2_0
			var name =
				( from i in namedIndexes
				  from n in names
				  where culture.CompareInfo.Compare( i.Key, n, compareOptions ) == 0
				  select i.Key ).SingleOrDefault();
#else
			string name = null;
			foreach( var pair in namedIndexes )
			{
				foreach( var n in names )
				{
					if( culture.CompareInfo.Compare( pair.Key, n, compareOptions ) == 0 )
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
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of object that is created
		/// and populated.</typeparam>
		/// <returns>The function delegate.</returns>
		protected virtual Func<ICsvReader, T> GetReadRecordFunc<T>() where T : class
		{
			var recordType = typeof( T );
			CreateReadRecordFunc( recordType, ( body, readerParameter ) => Expression.Lambda<Func<ICsvReader, T>>( body, readerParameter ).Compile() );

			return (Func<ICsvReader, T>)recordFuncs[recordType];
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <param name="recordType">The <see cref="Type"/> of object that is created
		/// and populated.</param>
		/// <returns>The function delegate.</returns>
		protected virtual Func<ICsvReader, object> GetReadRecordFunc( Type recordType )
		{
			CreateReadRecordFunc( recordType, ( body, readerParameter ) => Expression.Lambda<Func<ICsvReader, object>>( body, readerParameter ).Compile() );

			return (Func<ICsvReader, object>)recordFuncs[recordType];
		}

		/// <summary>
		/// Creates the read record func for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="recordType">Type of the record.</param>
		/// <param name="expressionCompiler">The expression compiler.</param>
		protected virtual void CreateReadRecordFunc( Type recordType, Func<Expression, ParameterExpression, Delegate> expressionCompiler )
		{
			if( recordFuncs.ContainsKey( recordType ) )
			{
				return;
			}

			var bindings = new List<MemberBinding>();
			var readerParameter = Expression.Parameter( typeof( ICsvReader ), "reader" );

			// If there is no property mappings yet, use attribute mappings.
			if( configuration.Properties.Count == 0 )
			{
				configuration.AttributeMapping( recordType );
			}

			AddPropertyBindings( readerParameter, configuration.Properties, bindings );

			foreach( var referenceMap in configuration.References )
			{
				var referenceReaderParameter = Expression.Parameter( typeof( ICsvReader ), "reader2" );
				var referenceBindings = new List<MemberBinding>();
				AddPropertyBindings( referenceReaderParameter, referenceMap.ReferenceProperties, referenceBindings );
				var referenceBody = Expression.MemberInit( Expression.New( referenceMap.Property.PropertyType ), referenceBindings );
				var referenceFunc = Expression.Lambda( referenceBody, referenceReaderParameter );
				var referenceCompiled = referenceFunc.Compile();
				var referenceCompiledMethod = referenceCompiled.GetType().GetMethod( "Invoke" );
				Expression referenceObjectExpression = Expression.Call( Expression.Constant( referenceCompiled ), referenceCompiledMethod, Expression.Constant( this ) );
				bindings.Add( Expression.Bind( referenceMap.Property, referenceObjectExpression ) );
			}

			var constructorExpression = configuration.Constructor ?? Expression.New( recordType );
			var body = Expression.MemberInit( constructorExpression, bindings );
			var func = expressionCompiler( body, readerParameter );
			recordFuncs[recordType] = func;

			#region This is the expression that is built:

			//
			// Func<CsvReader, T> func = reader => 
			// {
			//	foreach( var propertyMap in configuration.Properties )
			//	{
			//		string field = reader[index];
			//		object converted = ITypeConverter.ConvertFromString( field );
			//		T convertedAsType = converted as T;
			//		property.Property = convertedAsType;
			//	}
			//	
			//	foreach( var referenceMap in configuration.References )
			//	{
			//		Func<CsvReader, referenceMap.Property.PropertyType> func2 = reader2 =>
			//		{
			//			foreach( var property in referenceMap.ReferenceProperties )
			//			{
			//				string field = reader[index];
			//				object converted = ITypeConverter.ConvertFromString( field );
			//				T convertedAsType = converted as T;
			//				property.Property = convertedAsType;
			//			}
			//		};
			//		reference.Property = func2( (CsvReader)this );
			//	}
			// };
			//
			// The func can then be called:
			//
			// func( CsvReader reader );
			//

			#endregion
		}

		/// <summary>
		/// Adds a <see cref="MemberBinding"/> for each property for it's field.
		/// </summary>
		/// <param name="readerParameter">The reader parameter.</param>
		/// <param name="properties">The properties.</param>
		/// <param name="bindings">The bindings.</param>
		protected virtual void AddPropertyBindings( ParameterExpression readerParameter, CsvPropertyMapCollection properties, List<MemberBinding> bindings )
		{
			foreach( var propertyMap in properties )
			{
				if( propertyMap.ConvertUsingValue != null )
				{
					// The user is providing the expression to do the conversion.
					var exp = Expression.Invoke( propertyMap.ConvertUsingValue, Expression.Constant( this ) );
					bindings.Add( Expression.Bind( propertyMap.PropertyValue, exp ) );
					continue;
				}

				if( propertyMap.IgnoreValue )
				{
					// Skip ignored properties.
					continue;
				}

				if( propertyMap.TypeConverterValue == null || !propertyMap.TypeConverterValue.CanConvertFrom( typeof( string ) ) )
				{
					// Skip if the type isn't convertible.
					continue;
				}

				var index = propertyMap.IndexValue < 0 ? GetFieldIndex( propertyMap.NamesValue ) : propertyMap.IndexValue;
				if( index == -1 )
				{
					// Skip if the index was not found.
					continue;
				}

				// Get the field using the field index.
				var method = typeof( ICsvReaderRow ).GetProperty( "Item", typeof(string), new[] { typeof( int ) } ).GetGetMethod();
				Expression fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( index, typeof( int ) ) );

				// Convert the field.
				var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
				var culture = Expression.Constant( Configuration.UseInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture );

				// Create type converter expression.
				Expression typeConverterFieldExpression = Expression.Call( typeConverterExpression, "ConvertFromString", null, culture, fieldExpression );
				typeConverterFieldExpression = Expression.Convert( typeConverterFieldExpression, propertyMap.PropertyValue.PropertyType );

				if( propertyMap.IsDefaultValueSet )
				{
					// Create default value expression.
					Expression defaultValueExpression = Expression.Convert( Expression.Constant( propertyMap.DefaultValue ), propertyMap.PropertyValue.PropertyType );

					var checkFieldEmptyExpression = Expression.Equal( Expression.Convert( fieldExpression, typeof( string ) ), Expression.Constant( string.Empty, typeof( string ) ) );
					fieldExpression = Expression.Condition( checkFieldEmptyExpression, defaultValueExpression, typeConverterFieldExpression );
				}
				else
				{
					fieldExpression = typeConverterFieldExpression;
				}

				bindings.Add( Expression.Bind( propertyMap.PropertyValue, fieldExpression ) );
			}
		}
#endif
	}
}
