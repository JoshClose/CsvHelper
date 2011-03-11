#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

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

		/// <summary>
		/// Gets a value indicating if the CSV file has a header record.
		/// </summary>
		public virtual bool HasHeaderRecord { get; private set; }

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
		/// Gets a value indicating if strict reading is enabled.
		/// </summary>
		public virtual bool Strict { get; private set; }

		/// <summary>
		/// Gets the binding flags used to populate
		/// custom class objects.
		/// </summary>
		public virtual BindingFlags PropertyBindingFlags { get; private set; }

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
		public CsvReader( ICsvParser parser ) : this( parser, new CsvReaderOptions() ){}

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="ICsvParser" /> and <see cref="CsvReaderOptions" />.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> used to parse the CSV file.</param>
		/// <param name="options">The <see cref="CsvReaderOptions" /> used to read the parsed CSV file.</param>
		public CsvReader( ICsvParser parser, CsvReaderOptions options )
		{
			this.parser = parser;
			Strict = options.Strict;
			PropertyBindingFlags = options.PropertyBindingFlags;
			HasHeaderRecord = options.HasHeaderRecord;
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

			if( HasHeaderRecord && currentRecord == null )
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
			return currentRecord[index];
		}

		/// <summary>
		/// Gets the raw field at name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( string name )
		{
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
		/// Gets the raw field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <param name="field">The raw field.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( int index, out string field )
		{
			try
			{
				field = GetField( index );
			}
			catch
			{
				field = default( string );
				return false;
			}
			return true;
		}

		/// <summary>
		/// Gets the raw field at name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The raw field.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( string name, out string field )
		{
			try
			{
				field = GetField( name );
			}
			catch
			{
				field = default( string );
				return false;
			}
			return true;
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
			try
			{
				field = GetField<T>( index );
			}
			catch
			{
				field = default( T );
				return false;
			}
			return true;
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
			try
			{
				field = GetField<T>( name );
			}
			catch
			{
				field = default( T );
				return false;
			}
			return true;
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
			try
			{
				field = GetField<T>( index, converter );
			}
			catch
			{
				field = default( T );
				return false;
			}
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
			try
			{
				field = GetField<T>( name, converter );
			}
			catch
			{
				field = default( T );
				return false;
			}
			return true;
		}

		/// <summary>
		/// Gets the record converted into <see cref="Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="Type"/> T.</returns>
		public virtual T GetRecord<T>()
		{
			CheckDisposed();
			CheckHasBeenRead();

			return GetRecordFunc<T>()( this );
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		public virtual IList<T> GetRecords<T>()
		{
			CheckDisposed();

			var records = new List<T>();

			while( Read() )
			{
				var record = GetRecordFunc<T>()( this );
				records.Add( record );
			}

			return records;
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
			return converter.ConvertFrom( currentRecord[index] );
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
			if( !HasHeaderRecord )
			{
				throw new CsvReaderException( "There is no header record to determine the index by name." );
			}

			if( !namedIndexes.ContainsKey( name ) )
			{
				if( Strict )
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
		protected virtual Func<CsvReader, T> GetRecordFunc<T>()
		{
			var type = typeof( T );
			if( !recordFuncs.ContainsKey( type ) )
			{
				var bindings = new List<MemberBinding>();
				var recordType = typeof( T );
				var readerParameter = Expression.Parameter( GetType(), "reader" );
				foreach( var property in recordType.GetProperties( PropertyBindingFlags ) )
				{
					var csvFieldAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( property, false );
					int index;
					if( csvFieldAttribute != null )
					{
						if( csvFieldAttribute.Ignore )
						{
							// Skip the ignored properties.
							continue;
						}

						if( !String.IsNullOrEmpty( csvFieldAttribute.FieldName ) )
						{
							index = GetFieldIndex( csvFieldAttribute.FieldName );
							if( index < 0 )
							{
								// Skip if the index was not found.
								continue;
							}
						}
						else
						{
							index = csvFieldAttribute.FieldIndex;
						}
					}
					else
					{
						index = GetFieldIndex( property.Name );
						if( index < 0 )
						{
							// Skip if the index was not found.
							continue;
						}
					}

					// Get the field using the field index.
					var method = GetType().GetProperty( "Item", new[] { typeof( int ) } ).GetGetMethod();
					Expression fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( index, typeof( int ) ) );

					var typeConverter = ReflectionHelper.GetTypeConverter( property );

					if( typeConverter != null && typeConverter.CanConvertFrom( typeof( string ) ) )
					{
						// Use the TypeConverter from the attribute to convert the type.
						var typeConverterExpression = Expression.Constant( typeConverter );
						fieldExpression = Expression.Call( typeConverterExpression, "ConvertFromInvariantString", null, fieldExpression );
						fieldExpression = Expression.Convert( fieldExpression, property.PropertyType );
					}
					else if( property.PropertyType != typeof( string ) )
					{
						var formatProvider = Expression.Constant( CultureInfo.InvariantCulture, typeof( IFormatProvider ) );
						var parseMethod = property.PropertyType.GetMethod( "Parse", new[] { typeof( string ), typeof( IFormatProvider ) } );
						if( parseMethod != null )
						{
							// Use the types Parse method.
							fieldExpression = Expression.Call( null, parseMethod, fieldExpression, formatProvider );
						}
						else
						{
							// Use the default TypeConverter for the properties type.
							typeConverter = TypeDescriptor.GetConverter( property.PropertyType );
							if( !typeConverter.CanConvertFrom( typeof( string ) ) )
							{
								continue;
							}
							// Only convert if the convertable is capable.
							var typeConverterExpression = Expression.Constant( typeConverter );
							fieldExpression = Expression.Call( typeConverterExpression, "ConvertFromInvariantString", null, fieldExpression );
							fieldExpression = Expression.Convert( fieldExpression, property.PropertyType );
						}
					}

					bindings.Add( Expression.Bind( property, fieldExpression ) );
				}

				var body = Expression.MemberInit( Expression.New( recordType ), bindings );
				var func = Expression.Lambda<Func<CsvReader, T>>( body, readerParameter ).Compile();
				recordFuncs[type] = func;
			}

			return (Func<CsvReader, T>)recordFuncs[type];
		}
	}
}
