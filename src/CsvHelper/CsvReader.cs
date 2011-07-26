﻿#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using CsvHelper.Configuration;

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
		private readonly Dictionary<string, int> namedIndexes = new Dictionary<string, int>();
		private readonly Dictionary<Type, Delegate> recordFuncs = new Dictionary<Type, Delegate>();
		private CsvConfiguration configuration;

		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		public virtual CsvConfiguration Configuration
		{
			get { return configuration; }
			set { configuration = value; }
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
		/// Creates a new CSV reader using <see cref="CsvParser"/> as
		/// the default parser.
		/// </summary>
		/// <param name="reader"></param>
		public CsvReader( TextReader reader ) : this( new CsvParser( reader ) ){}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="ICsvParser" />.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> used to parse the CSV file.</param>
		public CsvReader( ICsvParser parser ) : this( parser, new CsvConfiguration() ){}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="ICsvParser"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser"/> used to parse the CSV file.</param>
		/// <param name="configuration">The <see cref="CsvConfiguration"/>.</param>
		public CsvReader( ICsvParser parser, CsvConfiguration configuration )
		{
			this.parser = parser;
			this.configuration = configuration;
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

			if( configuration.HasHeaderRecord && currentRecord == null )
			{
				headerRecord = parser.Read();
				ParseNamedIndexes();
			}

			currentRecord = parser.Read();

			hasBeenRead = true;

			return currentRecord != null;
		}

		/// <summary>
		/// Gets the raw field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
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
		/// Gets the raw string field at name.
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
		/// Gets the raw field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			return currentRecord[index];
		}

		/// <summary>
		/// Gets the raw field at name.
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
		/// Gets the field converted to <see cref="Type"/> T at index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( string name )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return GetField<T>( name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( int index, TypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			return (T)GetField( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <returns>The field converted to <see cref="Type"/> T.</returns>
		public virtual T GetField<T>( string name, TypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			return (T)GetField( name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return TryGetField( name, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at index
		/// using the specified <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, TypeConverter converter, out T field )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var rawField = currentRecord[index];
			if(!converter.IsValid( rawField ))
			{
				field = default( T );
				return false;
			}

			field = (T)GetField( index, converter );
			return true;
		}

		/// <summary>
		/// Gets the field converted to <see cref="Type"/> T at name
		/// using the specified <see cref="TypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, TypeConverter converter, out T field )
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
		/// Gets the record converted into <see cref="Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="Type"/> T.</returns>
		public virtual T GetRecord<T>() where T : class
		{
			CheckDisposed();
			CheckHasBeenRead();

			return GetReadRecordFunc<T>()( this );
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

			while( Read() )
			{
				yield return GetReadRecordFunc<T>()( this );
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
		}

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
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="TypeConverter"/>.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		protected virtual object GetField( int index, TypeConverter converter )
		{
			return converter.ConvertFromString( currentRecord[index] );
		}
        
		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="TypeConverter"/>.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="TypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		protected virtual object GetField( string name, TypeConverter converter )
		{
			var index = GetFieldIndex( name );
			return GetField( index, converter );
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="name">The name of the field to get the index for.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="CsvReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="CsvMissingFieldException">Thrown if there isn't a field with name.</exception>
		protected virtual int GetFieldIndex( string name )
		{
			if (!configuration.HasHeaderRecord)
			{
				throw new CsvReaderException( "There is no header record to determine the index by name." );
			}

			if( !namedIndexes.ContainsKey( name ) )
			{
				if( configuration.Strict )
				{
					// If we're in strict reading mode and the
					// named index isn't found, throw an exception.
					throw new CsvMissingFieldException( string.Format( "Field '{0}' does not exist in the CSV file.", name ) );
				}
				return -1;
			}

			return namedIndexes[name];
		}

		/// <summary>
		/// Parses the named indexes from the header record.
		/// </summary>
		protected virtual void ParseNamedIndexes()
		{
			for( var i = 0; i < headerRecord.Length; i++ )
			{
				var name = headerRecord[i];
				if( namedIndexes.ContainsKey( name ) )
				{
					throw new CsvReaderException( "The field header names must be unique." );
				}
				namedIndexes[name] = i;
			}
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of object that is created
		/// and populated.</typeparam>
		/// <returns>The function delegate.</returns>
		protected virtual Func<CsvReader, T> GetReadRecordFunc<T>() where T : class
		{
			var type = typeof( T );
			if( !recordFuncs.ContainsKey( type ) )
			{
				var bindings = new List<MemberBinding>();
				var recordType = typeof( T );
				var readerParameter = Expression.Parameter( GetType(), "reader" );

				// If there is no property mappings yet, use attribute mappings.
				if( configuration.Properties.Count == 0 )
				{
					configuration.AttributeMapping<T>();
				}

				foreach( var propertyMap in configuration.Properties )
				{
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

					var index = propertyMap.IndexValue < 0 ? GetFieldIndex( propertyMap.NameValue ) : propertyMap.IndexValue;
					if( index == -1 )
					{
						// Skip if the index was not found.
						continue;
					}

					// Get the field using the field index.
					var method = GetType().GetProperty( "Item", new[] { typeof( int ) } ).GetGetMethod();
					Expression fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( index, typeof( int ) ) );

					// Convert the field.
					var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
					fieldExpression = Expression.Call( typeConverterExpression, "ConvertFromString", null, fieldExpression );
					fieldExpression = Expression.Convert( fieldExpression, propertyMap.PropertyValue.PropertyType );

					bindings.Add( Expression.Bind( propertyMap.PropertyValue, fieldExpression ) );
				}

				var body = Expression.MemberInit( Expression.New( recordType ), bindings );
				var func = Expression.Lambda<Func<CsvReader, T>>( body, readerParameter ).Compile();
				recordFuncs[type] = func;
			}

			return (Func<CsvReader, T>)recordFuncs[type];
		}
	}
}
