﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Dynamic;

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="ICsvParser" />.
	/// </summary>
	public class CsvReader<TParser> : ICsvReader<TParser> where TParser : IParser
	{
	    private readonly bool leaveOpen;
		private bool disposed;
		private bool hasBeenRead;
		private string[] currentRecord;
		private string[] headerRecord;
		private int currentIndex = -1;
		private int columnCount;
		private readonly Dictionary<string, List<int>> namedIndexes = new Dictionary<string, List<int>>();
	    private readonly Dictionary<string, Tuple<string, int>> namedIndexCache = new Dictionary<string, Tuple<string, int>>();
        private readonly Dictionary<Type, Delegate> recordFuncs = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets the Configuration.
		/// </summary>
		public virtual ICsvReaderConfiguration Configuration { get; }

		/// <summary>
		/// Gets the parser.
		/// </summary>
		public virtual TParser Parser { get; }

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		public virtual string[] FieldHeaders
		{
			get
			{
				if( headerRecord == null )
				{
					throw new CsvReaderException( "You must call ReadHeader or Read before accessing the field headers." );
				}

				return headerRecord;
			}
		}

		/// <summary>
		/// Get the current record;
		/// </summary>
		public virtual string[] CurrentRecord => currentRecord;

		/// <summary>
		/// Gets the current row.
		/// </summary>
		public int Row => Parser.Row;

        /// <summary>
        /// Creates a new CSV reader using the given <see cref="ICsvParser"/>.
        /// </summary>
        /// <param name="parser">The <see cref="ICsvParser" /> used to parse the CSV file.</param>
        /// <param name="leaveOpen">true to leave the parser open after the CsvReader object is disposed, otherwise false.</param>
        public CsvReader( TParser parser, bool leaveOpen )
		{
			if( parser == null )
			{
				throw new ArgumentNullException( nameof( parser ) );
			}

			if( parser.Configuration == null )
			{
				throw new CsvConfigurationException( "The given parser has no Configuration." );
			}

			if( !( parser.Configuration is ICsvReaderConfiguration ) )
			{
				throw new CsvConfigurationException( "The given parser does not have a Configuration that works with the reader." );
			}

			Parser = parser;
			Configuration = (ICsvReaderConfiguration)parser.Configuration;
		    this.leaveOpen = leaveOpen;
		}

		/// <summary>
		/// Reads the header field without reading the first row.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual bool ReadHeader()
		{
			if( !Configuration.HasHeaderRecord )
			{
				throw new CsvReaderException( "Configuration.HasHeaderRecord is false." );
			}

			headerRecord = currentRecord;
			currentRecord = null;
			ParseNamedIndexes();

			return headerRecord != null;
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
			do
			{
				currentRecord = Parser.Read();
			}
			while( ShouldSkipRecord() );

			currentIndex = -1;
			hasBeenRead = true;

			if( Configuration.DetectColumnCountChanges && currentRecord != null )
			{
				if( columnCount > 0 && columnCount != currentRecord.Length )
				{
					var csvException = new CsvBadDataException( "An inconsistent number of columns has been detected." );
					ExceptionHelper.AddExceptionData( csvException, Row, null, currentIndex, namedIndexes, currentRecord );

					if( Configuration.IgnoreReadingExceptions )
					{
						Configuration.ReadingExceptionCallback?.Invoke( csvException, this );
					}
					else
					{
						throw csvException;
					}
				}

				columnCount = currentRecord.Length;
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
			CheckHasBeenRead();

			// Set the current index being used so we
			// have more information if an error occurs
			// when reading records.
			currentIndex = index;

			if( index >= currentRecord.Length )
			{
				if( Configuration.WillThrowOnMissingField && Configuration.IgnoreBlankLines )
				{
					var ex = new CsvMissingFieldException( $"Field at index '{index}' does not exist." );
					ExceptionHelper.AddExceptionData( ex, Row, null, index, namedIndexes, currentRecord );

					throw ex;
				}

				return default( string );
			}

			var field = currentRecord[index];
			if( Configuration.TrimFields )
			{
				field = field?.Trim();
			}

			return field;
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField( string name )
		{
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
		/// <param name="type">The type of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, int index )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return GetField( type, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, string name )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return GetField( type, name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, string name, int index )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return GetField( type, name, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, int index, ITypeConverter converter )
		{
			CheckHasBeenRead();

			var propertyMapData = new CsvPropertyMapData( null )
			{
				Index = index,
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = Configuration.CultureInfo }
			};
			propertyMapData.TypeConverterOptions = TypeConverterOptions.Merge( Configuration.TypeConverterOptionsFactory.GetOptions( type ), propertyMapData.TypeConverterOptions );

			var field = GetField( index );
			return converter.ConvertFromString( field, this, propertyMapData );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, string name, ITypeConverter converter )
		{
			CheckHasBeenRead();

			var index = GetFieldIndex( name );
			return GetField( type, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField( Type type, string name, int index, ITypeConverter converter )
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index );
			return GetField( type, fieldIndex, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>( int index )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>( string name )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position
		/// (column) name and the index instance of that field. The index
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns></returns>
		public virtual T GetField<T>( string name, int index )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return GetField<T>( name, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>( int index, ITypeConverter converter )
		{
			CheckHasBeenRead();

			if( index >= currentRecord.Length || index < 0 )
			{
				if( Configuration.WillThrowOnMissingField )
				{
					var ex = new CsvMissingFieldException( $"Field at index '{index}' does not exist." );
					ExceptionHelper.AddExceptionData( ex, Row, typeof( T ), index, namedIndexes, currentRecord );

					throw ex;
				}

				return default( T );
			}

			return (T)GetField( typeof( T ), index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>( string name, ITypeConverter converter )
		{
			CheckHasBeenRead();

			var index = GetFieldIndex( name );
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position
		/// (column) name and the index instance of that field. The index
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>( string name, int index, ITypeConverter converter )
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index );
			return GetField<T>( fieldIndex, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>( int index ) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>( string name ) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( name, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position
		/// (column) name and the index instance of that field. The index
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>( string name, int index ) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>( name, index, converter );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, int index, out object field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return TryGetField( type, index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, string name, out object field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return TryGetField( type, name, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position
		/// (column) name and the index instance of that field. The index
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, string name, int index, out object field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter( type );
			return TryGetField( type, name, index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, int index, ITypeConverter converter, out object field )
		{
			CheckHasBeenRead();

			// DateTimeConverter.ConvertFrom will successfully convert
			// a white space string to a DateTime.MinValue instead of
			// returning null, so we need to handle this special case.
			if( converter is DateTimeConverter )
			{
				if( StringHelper.IsNullOrWhiteSpace( currentRecord[index] ) )
				{
					field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance( type ) : null;
					return false;
				}
			}

			// TypeConverter.IsValid() just wraps a
			// ConvertFrom() call in a try/catch, so lets not
			// do it twice and just do it ourselves.
			try
			{
				field = GetField( type, index, converter );
				return true;
			}
			catch
			{
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance( type ) : null;
				return false;
			}
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, string name, ITypeConverter converter, out object field )
		{
			CheckHasBeenRead();

			var index = GetFieldIndex( name, isTryGet: true );
			if( index == -1 )
			{
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance( type ) : null;
				return false;
			}

			return TryGetField( type, index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField( Type type, string name, int index, ITypeConverter converter, out object field )
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index, true );
			if( fieldIndex == -1 )
			{
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance( type ) : null;
				return false;
			}

			return TryGetField( type, fieldIndex, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, out T field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, out T field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( name, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position
		/// (column) name and the index instance of that field. The index
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, int index, out T field )
		{
			CheckHasBeenRead();

			var converter = TypeConverterFactory.GetConverter<T>();
			return TryGetField( name, index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( int index, ITypeConverter converter, out T field )
		{
			CheckHasBeenRead();

			// TypeConverter.IsValid() just wraps a
			// ConvertFrom() call in a try/catch, so lets not
			// do it twice and just do it ourselves.
			try
			{
				field = GetField<T>( index, converter );
				return true;
			}
			catch
			{
				field = default( T );
				return false;
			}
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, ITypeConverter converter, out T field )
		{
			CheckHasBeenRead();

			var index = GetFieldIndex( name, isTryGet: true );
			if( index == -1 )
			{
				field = default( T );
				return false;
			}

			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>( string name, int index, ITypeConverter converter, out T field )
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex( name, index, true );
			if( fieldIndex == -1 )
			{
				field = default( T );
				return false;
			}

			return TryGetField( fieldIndex, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>( int index, out T field ) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField( index, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>( string name, out T field ) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField( name, converter, out field );
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>( string name, int index, out T field ) where TConverter : ITypeConverter
		{
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
			CheckHasBeenRead();

			return IsRecordEmpty( true );
		}

		/// <summary>
		/// Gets the record converted into <see cref="System.Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetRecord<T>()
		{
			CheckHasBeenRead();

			if( headerRecord == null && Configuration.HasHeaderRecord )
			{
				ReadHeader();

				if( !Read() )
				{
					return default( T );
				}
			}

			T record;
			try
			{
				record = CreateRecord<T>();
			}
			catch( Exception ex )
			{
				var csvHelperException = ex as CsvHelperException ?? new CsvReaderException( "An unexpected error occurred.", ex );
				ExceptionHelper.AddExceptionData( csvHelperException, Row, typeof( T ), currentIndex, namedIndexes, currentRecord );

				throw csvHelperException;
			}

			return record;
		}

		/// <summary>
		/// Gets the record.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the record.</param>
		/// <returns>The record.</returns>
		public virtual object GetRecord( Type type )
		{
			CheckHasBeenRead();

			if( headerRecord == null && Configuration.HasHeaderRecord )
			{
				ReadHeader();

				if( !Read() )
				{
					return null;
				}
			}

			object record;
			try
			{
				record = CreateRecord( type );
			}
			catch( Exception ex )
			{
				var csvHelperException = ex as CsvHelperException ?? new CsvReaderException( "An unexpected error occurred.", ex );
				ExceptionHelper.AddExceptionData( csvHelperException, Row, type, currentIndex, namedIndexes, currentRecord );

				throw csvHelperException;
			}

			return record;
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		public virtual IEnumerable<T> GetRecords<T>()
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if( Configuration.HasHeaderRecord && headerRecord == null )
			{
				if( !Read() )
				{
					yield break;
				}

				ReadHeader();
			}

			while( Read() )
			{
				T record;
				try
				{
					record = CreateRecord<T>();
				}
				catch( Exception ex )
				{
					var csvHelperException = ex as CsvHelperException ?? new CsvReaderException( "An unexpected error occurred.", ex );
					ExceptionHelper.AddExceptionData( csvHelperException, Row, typeof( T ), currentIndex, namedIndexes, currentRecord );

					if( Configuration.IgnoreReadingExceptions )
					{
						Configuration.ReadingExceptionCallback?.Invoke( csvHelperException, this );

						continue;
					}

					throw csvHelperException;
				}

				yield return record;
			}
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the record.</param>
		/// <returns>An <see cref="IList{Object}" /> of records.</returns>
		public virtual IEnumerable<object> GetRecords( Type type )
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if( Configuration.HasHeaderRecord && headerRecord == null )
			{
				if( !Read() )
				{
					yield break;
				}

				ReadHeader();
			}

			while( Read() )
			{
				object record;
				try
				{
					record = CreateRecord( type );
				}
				catch( Exception ex )
				{
					var csvHelperException = ex as CsvHelperException ?? new CsvReaderException( "An unexpected error occurred.", ex );
					ExceptionHelper.AddExceptionData( csvHelperException, Row, type, currentIndex, namedIndexes, currentRecord );

					if( Configuration.IgnoreReadingExceptions )
					{
						Configuration.ReadingExceptionCallback?.Invoke( csvHelperException, this );

						continue;
					}

					throw csvHelperException;
				}

				yield return record;
			}
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache{T}"/> needs to be called to update the
		/// record cache.
		/// </summary>
		public virtual void ClearRecordCache<T>()
		{
			ClearRecordCache( typeof( T ) );
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache(System.Type)"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <param name="type">The type to invalidate.</param>
		public virtual void ClearRecordCache( Type type )
		{
			recordFuncs.Remove( type );
		}

		/// <summary>
		/// Clears the record cache for all types. After <see cref="ICsvReaderRow.GetRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ClearRecordCache()"/> needs to be called to update the
		/// record cache.
		/// </summary>
		public virtual void ClearRecordCache()
		{
			recordFuncs.Clear();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose( !leaveOpen );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose( bool disposing )
		{
			if( disposed )
			{
				return;
			}

			if( disposing )
			{
				Parser?.Dispose();
			}

			disposed = true;
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
			if( checkHasBeenRead )
			{
				CheckHasBeenRead();
			}

			if( currentRecord == null )
			{
				return false;
			}

			return currentRecord.All( GetEmtpyStringMethod() );
		}

		/// <summary>
		/// Gets a function to test for an empty string.
		/// Will check <see cref="CsvConfiguration.TrimFields" /> when making its decision.
		/// </summary>
		/// <returns>The function to test for an empty string.</returns>
		protected virtual Func<string, bool> GetEmtpyStringMethod()
		{
			if( !Configuration.TrimFields )
			{
				return string.IsNullOrEmpty;
			}

			return string.IsNullOrWhiteSpace;
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="name">The name of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <param name="isTryGet">A value indicating if the call was initiated from a TryGet.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="CsvReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="CsvMissingFieldException">Thrown if there isn't a field with name.</exception>
		protected virtual int GetFieldIndex( string name, int index = 0, bool isTryGet = false )
		{
			return GetFieldIndex( new[] { name }, index, isTryGet );
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="names">The possible names of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <param name="isTryGet">A value indicating if the call was initiated from a TryGet.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="CsvReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="CsvMissingFieldException">Thrown if there isn't a field with name.</exception>
		protected virtual int GetFieldIndex( string[] names, int index = 0, bool isTryGet = false )
		{
			if( names == null )
			{
				throw new ArgumentNullException( nameof( names ) );
			}

			if( !Configuration.HasHeaderRecord )
			{
				throw new CsvReaderException( "There is no header record to determine the index by name." );
			}

            // Caching the named index speeds up mappings that use ConvertUsing tremendously.
		    var nameKey = string.Join( "_", names ) + index;
		    if( namedIndexCache.ContainsKey( nameKey ) )
		    {
		        var tuple = namedIndexCache[nameKey];
		        return namedIndexes[tuple.Item1][tuple.Item2];
		    }

			string name = null;
			foreach( var pair in namedIndexes )
			{
				var propertyName = Configuration.PrepareHeaderForMatch( pair.Key );
				foreach( var n in names )
				{
					var fieldName = Configuration.PrepareHeaderForMatch( n );
					if( Configuration.CultureInfo.CompareInfo.Compare( propertyName, fieldName, CompareOptions.None ) == 0 )
					{
						name = pair.Key;
					}
				}
			}

			if( name == null || index >= namedIndexes[name].Count )
			{
				if( Configuration.WillThrowOnMissingField && !isTryGet )
				{
					// If we're in strict reading mode and the
					// named index isn't found, throw an exception.
					var namesJoined = $"'{string.Join( "', '", names )}'";
					var ex = new CsvMissingFieldException( $"Fields {namesJoined} do not exist in the CSV file." );
					ExceptionHelper.AddExceptionData( ex, Row, null, currentIndex, namedIndexes, currentRecord );

					throw ex;
				}

				return -1;
			}

		    namedIndexCache.Add( nameKey, new Tuple<string, int>( name, index ) );

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

		/// <summary>
		/// Checks if the current record should be skipped or not.
		/// </summary>
		/// <returns><c>true</c> if the current record should be skipped, <c>false</c> otherwise.</returns>
		protected virtual bool ShouldSkipRecord()
		{
			if( currentRecord == null )
			{
				return false;
			}

			return Configuration.ShouldSkipRecord != null
				? Configuration.ShouldSkipRecord( currentRecord ) || ( Configuration.SkipEmptyRecords && IsRecordEmpty( false ) )
				: Configuration.SkipEmptyRecords && IsRecordEmpty( false );
		}

		/// <summary>
		/// Creates the record for the given type.
		/// </summary>
		/// <typeparam name="T">The type of record to create.</typeparam>
		/// <returns>The created record.</returns>
		protected virtual T CreateRecord<T>()
		{
			// If the type is an object, a dynamic
			// object will be created. That is the
			// only way we can dynamically add properties
			// to a type of object.
			if( typeof( T ) == typeof( object ) )
			{
				return CreateDynamic();
			}

			return GetReadRecordFunc<T>()();
		}

		/// <summary>
		/// Creates the record for the given type.
		/// </summary>
		/// <param name="type">The type of record to create.</param>
		/// <returns>The created record.</returns>
		protected virtual object CreateRecord( Type type )
		{
			// If the type is an object, a dynamic
			// object will be created. That is the
			// only way we can dynamically add properties
			// to a type of object.
			if( type == typeof( object ) )
			{
				return CreateDynamic();
			}

			try
			{
				return GetReadRecordFunc( type ).DynamicInvoke();
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of object that is created
		/// and populated.</typeparam>
		/// <returns>The function delegate.</returns>
		protected virtual Func<T> GetReadRecordFunc<T>()
		{
			var recordType = typeof( T );
			CreateReadRecordFunc( recordType );

			return (Func<T>)recordFuncs[recordType];
		}

		/// <summary>
		/// Gets the function delegate used to populate
		/// a custom class object with data from the reader.
		/// </summary>
		/// <param name="recordType">The <see cref="System.Type"/> of object that is created
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
		protected virtual void CreateReadRecordFunc( Type recordType )
		{
			if( recordFuncs.ContainsKey( recordType ) )
			{
				return;
			}

			if( Configuration.Maps[recordType] == null )
			{
				Configuration.Maps.Add( Configuration.AutoMap( recordType ) );
			}

			if( recordType.GetTypeInfo().IsPrimitive )
			{
				CreateFuncForPrimitive( recordType );
			}
			else
			{
				CreateFuncForObject( recordType );
			}
		}

		/// <summary>
		/// Creates the function for an object.
		/// </summary>
		/// <param name="recordType">The type of object to create the function for.</param>
		protected virtual void CreateFuncForObject( Type recordType )
		{
			var bindings = new List<MemberBinding>();

			CreatePropertyBindingsForMapping( Configuration.Maps[recordType], recordType, bindings );

			if( bindings.Count == 0 )
			{
				throw new CsvReaderException( $"No properties are mapped for type '{recordType.FullName}'." );
			}

			Expression body;
			var constructorExpression = Configuration.Maps[recordType].Constructor;
			if( constructorExpression is NewExpression )
			{
				body = Expression.MemberInit( (NewExpression)constructorExpression, bindings );
			}
			else if( constructorExpression is MemberInitExpression )
			{
				var memberInitExpression = (MemberInitExpression)constructorExpression;
				var defaultBindings = memberInitExpression.Bindings.ToList();
				defaultBindings.AddRange( bindings );
				body = Expression.MemberInit( memberInitExpression.NewExpression, defaultBindings );
			}
			else
			{
				body = Expression.MemberInit( Expression.New( recordType ), bindings );
			}

			var funcType = typeof( Func<> ).MakeGenericType( recordType );
			recordFuncs[recordType] = Expression.Lambda( funcType, body ).Compile();
		}

		/// <summary>
		/// Creates the function for a primitive.
		/// </summary>
		/// <param name="recordType">The type of the primitive to create the function for.</param>
		protected virtual void CreateFuncForPrimitive( Type recordType )
		{
			var method = typeof( ICsvReaderRow ).GetProperty( "Item", typeof( string ), new[] { typeof( int ) } ).GetGetMethod();
			Expression fieldExpression = Expression.Call( Expression.Constant( this ), method, Expression.Constant( 0, typeof( int ) ) );

			var propertyMapData = new CsvPropertyMapData( null )
			{
				Index = 0,
				TypeConverter = TypeConverterFactory.GetConverter( recordType ),
				TypeConverterOptions = { CultureInfo = Configuration.CultureInfo }
			};
			propertyMapData.TypeConverterOptions = TypeConverterOptions.Merge( Configuration.TypeConverterOptionsFactory.GetOptions( recordType ), propertyMapData.TypeConverterOptions );

			fieldExpression = Expression.Call( Expression.Constant( propertyMapData.TypeConverter ), "ConvertFromString", null, fieldExpression, Expression.Constant( this ), Expression.Constant( propertyMapData ) );
			fieldExpression = Expression.Convert( fieldExpression, recordType );

			var funcType = typeof( Func<> ).MakeGenericType( recordType );
			recordFuncs[recordType] = Expression.Lambda( funcType, fieldExpression ).Compile();
		}

		/// <summary>
		/// Creates the property/field bindings for the given <see cref="CsvClassMap"/>.
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
				CreatePropertyBindingsForMapping( referenceMap.Data.Mapping, referenceMap.Data.Member.MemberType(), referenceBindings );

				Expression referenceBody;
				var constructorExpression = referenceMap.Data.Mapping.Constructor;
				if( constructorExpression is NewExpression )
				{
					referenceBody = Expression.MemberInit( (NewExpression)constructorExpression, referenceBindings );
				}
				else if( constructorExpression is MemberInitExpression )
				{
					var memberInitExpression = (MemberInitExpression)constructorExpression;
					var defaultBindings = memberInitExpression.Bindings.ToList();
					defaultBindings.AddRange( referenceBindings );
					referenceBody = Expression.MemberInit( memberInitExpression.NewExpression, defaultBindings );
				}
				else
				{
					referenceBody = Expression.MemberInit( Expression.New( referenceMap.Data.Member.MemberType() ), referenceBindings );
				}

				bindings.Add( Expression.Bind( referenceMap.Data.Member, referenceBody ) );
			}
		}

		/// <summary>
		/// Adds a <see cref="MemberBinding"/> for each property/field for it's field.
		/// </summary>
		/// <param name="members">The properties/fields to add bindings for.</param>
		/// <param name="bindings">The bindings that will be added to from the properties.</param>
		protected virtual void AddPropertyBindings( CsvPropertyMapCollection members, List<MemberBinding> bindings )
		{
			foreach( var propertyMap in members )
			{
				if( propertyMap.Data.ConvertExpression != null )
				{
					// The user is providing the expression to do the conversion.
					Expression exp = Expression.Invoke( propertyMap.Data.ConvertExpression, Expression.Constant( this ) );
					exp = Expression.Convert( exp, propertyMap.Data.Member.MemberType() );
					bindings.Add( Expression.Bind( propertyMap.Data.Member, exp ) );
					continue;
				}

				if( !CanRead( propertyMap ) )
				{
					continue;
				}

				if( propertyMap.Data.TypeConverter == null )
				{
					// Skip if the type isn't convertible.
					continue;
				}

				int index;
				if( propertyMap.Data.IsNameSet || Configuration.HasHeaderRecord && !propertyMap.Data.IsIndexSet )
				{
					// Use the name.
					index = GetFieldIndex( propertyMap.Data.Names.ToArray(), propertyMap.Data.NameIndex );
					if( index == -1 )
					{
						// Skip if the index was not found.
						continue;
					}
				}
				else
				{
					// Use the index.
					index = propertyMap.Data.Index;
				}

				// Get the field using the field index.
				var method = typeof( ICsvReaderRow ).GetProperty( "Item", typeof( string ), new[] { typeof( int ) } ).GetGetMethod();
				Expression fieldExpression = Expression.Call( Expression.Constant( this ), method, Expression.Constant( index, typeof( int ) ) );

				// Convert the field.
				var typeConverterExpression = Expression.Constant( propertyMap.Data.TypeConverter );
				if( propertyMap.Data.TypeConverterOptions.CultureInfo == null )
				{
					propertyMap.Data.TypeConverterOptions.CultureInfo = Configuration.CultureInfo;
				}

				propertyMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( Configuration.TypeConverterOptionsFactory.GetOptions( propertyMap.Data.Member.MemberType() ), propertyMap.Data.TypeConverterOptions );

				// Create type converter expression.
				Expression typeConverterFieldExpression = Expression.Call( typeConverterExpression, "ConvertFromString", null, fieldExpression, Expression.Constant( this ), Expression.Constant( propertyMap.Data ) );
				typeConverterFieldExpression = Expression.Convert( typeConverterFieldExpression, propertyMap.Data.Member.MemberType() );

				if( propertyMap.Data.IsConstantSet )
				{
					fieldExpression = Expression.Constant( propertyMap.Data.Constant );
				}
				else if( propertyMap.Data.IsDefaultSet )
				{
					// Create default value expression.
					Expression defaultValueExpression = Expression.Convert( Expression.Constant( propertyMap.Data.Default ), propertyMap.Data.Member.MemberType() );

					// If null, use string.Empty.
					var coalesceExpression = Expression.Coalesce( fieldExpression, Expression.Constant( string.Empty ) );

					// Check if the field is an empty string.
					var checkFieldEmptyExpression = Expression.Equal( Expression.Convert( coalesceExpression, typeof( string ) ), Expression.Constant( string.Empty, typeof( string ) ) );

					// Use a default value if the field is an empty string.
					fieldExpression = Expression.Condition( checkFieldEmptyExpression, defaultValueExpression, typeConverterFieldExpression );
				}
				else
				{
					fieldExpression = typeConverterFieldExpression;
				}

				bindings.Add( Expression.Bind( propertyMap.Data.Member, fieldExpression ) );
			}
		}

		/// <summary>
		/// Determines if the property/field for the <see cref="CsvPropertyMap"/>
		/// can be read.
		/// </summary>
		/// <param name="propertyMap">The property/field map.</param>
		/// <returns>A value indicating of the property/field can be read. True if it can, otherwise false.</returns>
		protected virtual bool CanRead( CsvPropertyMap propertyMap )
		{
			var cantRead =
				// Ignored property/field;
				propertyMap.Data.Ignore;

			var property = propertyMap.Data.Member as PropertyInfo;
			if( property != null )
			{
				cantRead = cantRead ||
				// Properties that don't have a public setter
				// and we are honoring the accessor modifier.
				property.GetSetMethod() == null && !Configuration.IncludePrivateMembers ||
				// Properties that don't have a setter at all.
				property.GetSetMethod( true ) == null;
			}

			return !cantRead;
		}

		/// <summary>
		/// Determines if the property/field for the <see cref="CsvPropertyReferenceMap"/>
		/// can be read.
		/// </summary>
		/// <param name="propertyReferenceMap">The reference map.</param>
		/// <returns>A value indicating of the property/field can be read. True if it can, otherwise false.</returns>
		protected virtual bool CanRead( CsvPropertyReferenceMap propertyReferenceMap )
		{
			var cantRead = false;

			var property = propertyReferenceMap.Data.Member as PropertyInfo;
			if( property != null )
			{
				cantRead =
					// Properties that don't have a public setter
					// and we are honoring the accessor modifier.
					property.GetSetMethod() == null && !Configuration.IncludePrivateMembers ||
					// Properties that don't have a setter at all.
					property.GetSetMethod( true ) == null;
			}

			return !cantRead;
		}

		/// <summary>
		/// Creates a dynamic object from the current record.
		/// </summary>
		/// <returns>The dynamic object.</returns>
		protected virtual dynamic CreateDynamic()
		{
			var obj = new ExpandoObject();
			var dict = obj as IDictionary<string, object>;
			if( headerRecord != null )
			{
				var length = Math.Min( headerRecord.Length, currentRecord.Length );
				for( var i = 0; i < length; i++ )
				{
					var header = headerRecord[i];
					var field = GetField( i );
					dict.Add( header, field );
				}
			}
			else
			{
				for( var i = 0; i < currentRecord.Length; i++ )
				{
					var propertyName = "Field" + ( i + 1 );
					var field = GetField( i );
					dict.Add( propertyName, field );
				}
			}

			return obj;
		}
	}
}
