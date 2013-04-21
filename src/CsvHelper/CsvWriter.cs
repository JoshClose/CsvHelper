// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if !NET_2_0
using System.Linq;
using System.Linq.Expressions;
#endif
using System.Reflection;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if NET_2_0
using CsvHelper.MissingFrom20;
#endif
#if WINRT_4_5
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper
{
	/// <summary>
	/// Used to write CSV files.
	/// </summary>
	public class CsvWriter : ICsvWriter
	{
		private bool disposed;
		private readonly List<string> currentRecord = new List<string>();
		private TextWriter writer;
#if !NET_2_0
		private bool hasHeaderBeenWritten;
		private bool hasRecordBeenWritten;
		private readonly Dictionary<Type, Delegate> typeActions = new Dictionary<Type, Delegate>();
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
		/// Creates a new CSV writer using the given <see cref="StreamWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : this( writer, new CsvConfiguration() ) {}

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="StreamWriter"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="StreamWriter"/> use to write the CSV file.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvWriter( TextWriter writer, CsvConfiguration configuration )
		{
			if( writer == null )
			{
				throw new ArgumentNullException( "writer" );
			}
			if( configuration == null )
			{
				throw new ArgumentNullException( "configuration" );
			}

			this.writer = writer;
			this.configuration = configuration;
		}

		/// <summary>
		/// Writes the field to the CSV file. The field
		/// may get quotes added to it.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField( string field )
		{
			var shouldQuote = configuration.QuoteAllFields;

			if( !configuration.QuoteNoFields && !string.IsNullOrEmpty( field ) )
			{
				var hasQuote = false;
#if NET_2_0
				if( EnumerableHelper.Contains( field, configuration.Quote ) )
#elif WINRT_4_5
				if( field.Contains( configuration.Quote.ToString() ) )
#else
				if( field.Contains( configuration.Quote ) )
#endif
				{
					// All quotes must be doubled.
					field = field.Replace( configuration.Quote.ToString(), string.Concat( configuration.Quote, configuration.Quote ) );
					hasQuote = true;
				}

				if( hasQuote ||
				    field[0] == ' ' ||
				    field[field.Length - 1] == ' ' ||
				    field.Contains( configuration.Delimiter ) ||
				    field.Contains( "\n" ) ||
				    field.Contains( "\r" ) )
				{
					shouldQuote = true;
				}
			}

			WriteField( field, shouldQuote );
		}

		/// <summary>
		/// Writes the field to the CSV file. This will
		/// ignore any need to quote and ignore the
		/// <see cref="CsvConfiguration.QuoteAllFields"/>
		/// and just quote based on the shouldQuote
		/// parameter.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		/// <param name="shouldQuote">True to quote the field, otherwise false.</param>
		public virtual void WriteField( string field, bool shouldQuote )
		{
			if( shouldQuote )
			{
				field = field ?? string.Empty;
				field = new StringBuilder( field.Length + 2 )
					.Append( configuration.Quote )
					.Append( field )
					.Append( configuration.Quote )
					.ToString();
			}

			currentRecord.Add( field );
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField<T>( T field )
		{
			CheckDisposed();

			var type = typeof( T );
			if( type == typeof( string ) )
			{
				WriteField( field as string );
			}
			else
			{
				var converter = TypeConverterFactory.GetConverter<T>();
				WriteField( field, converter );
			}
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		/// <param name="converter">The converter used to convert the field into a string.</param>
		public virtual void WriteField<T>( T field, ITypeConverter converter )
		{
			CheckDisposed();

			var fieldString = converter.ConvertToString( Configuration.CultureInfo, field );
			WriteField( fieldString );
		}

		/// <summary>
		/// Writes the field to the CSV file
		/// using the given <see cref="ITypeConverter"/>.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <typeparam name="TConverter">The type of the converter.</typeparam>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField<T, TConverter>( T field )
		{
			CheckDisposed();

			var converter = TypeConverterFactory.GetConverter<TConverter>();
			WriteField( field, converter );
		}

		/// <summary>
		/// Ends writing of the current record
		/// and starts a new record. This is used
		/// when manually writing records with <see cref="ICsvWriter.WriteField{T}" />
		/// </summary>
		public virtual void NextRecord()
		{
			CheckDisposed();

			var record = string.Join( configuration.Delimiter, currentRecord.ToArray() );
			writer.WriteLine( record );
			currentRecord.Clear();
		}

#if !NET_2_0
		/// <summary>
		/// Writes the header record from the given properties.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		public virtual void WriteHeader<T>() where T : class
		{
			WriteHeader( typeof( T ) );
		}

		/// <summary>
		/// Writes the header record from the given properties.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		public virtual void WriteHeader( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			if( !configuration.HasHeaderRecord )
			{
				throw new CsvWriterException( "Configuration.HasHeaderRecord is false. This will need to be enabled to write the header." );
			}

			if( hasHeaderBeenWritten )
			{
				throw new CsvWriterException( "The header record has already been written. You can't write it more than once." );
			}

			if( hasRecordBeenWritten )
			{
				throw new CsvWriterException( "Records have already been written. You can't write the header after writing records has started." );
			}

			if( configuration.Mapping == null )
			{
				// TODO: auto class mapping
				throw new CsvConfigurationException( "No mapping has been created. Use Configuration.ClassMapping to create a map." );
			}

			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, configuration.Mapping );

			foreach( var property in properties )
			{
				if( !property.IgnoreValue )
				{
					WriteField( property.NameValue );
				}
			}

			NextRecord();

			hasHeaderBeenWritten = true;
		}

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		public virtual void WriteRecord<T>( T record ) where T : class
		{
			CheckDisposed();

			try
			{
				GetWriteRecordAction<T>()( record );
			}
			catch( Exception ex )
			{
				ExceptionHelper.AddExceptionDataMessage( ex, null, typeof( T ), null, null, null );
				throw;
			}

			hasRecordBeenWritten = true;

			NextRecord();
		}

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		/// <param name="record">The record to write.</param>
		public virtual void WriteRecord( Type type, object record )
		{
			CheckDisposed();

			try
			{
				GetWriteRecordAction( type ).DynamicInvoke( record );
			}
			catch( Exception ex )
			{
				ExceptionHelper.AddExceptionDataMessage( ex, null, type, null, null, null );
				throw;
			}

			hasRecordBeenWritten = true;

			NextRecord();
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords<T>( IEnumerable<T> records ) where T : class
		{
			CheckDisposed();

			if( configuration.HasHeaderRecord )
			{
				WriteHeader<T>();
			}

			foreach( var record in records )
			{
				try
				{
					GetWriteRecordAction<T>()( record );
				}
				catch( Exception ex )
				{
					ExceptionHelper.AddExceptionDataMessage( ex, null, typeof( T ), null, null, null );
					throw;
				}
				NextRecord();
			}
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords( Type type, IEnumerable<object> records )
		{
			CheckDisposed();

			if( configuration.HasHeaderRecord )
			{
				WriteHeader( type );
			}

			foreach( var record in records )
			{
				try
				{
					GetWriteRecordAction( type ).DynamicInvoke( this, record );
				}
				catch( Exception ex )
				{
					ExceptionHelper.AddExceptionDataMessage( ex, null, type, null, null, null );
					throw;
				}
				NextRecord();
			}
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvWriter.WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvWriter.ClearRecordCache{T}"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		public virtual void ClearRecordCache<T>() where T : class
		{
			ClearRecordCache( typeof( T ) );
		}

		/// <summary>
		/// Clears the record cache for the given type. After <see cref="ICsvWriter.WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvWriter.ClearRecordCache(System.Type)"/> needs to be called to update the
		/// record cache.
		/// </summary>
		/// <param name="type">The record type.</param>
		public virtual void ClearRecordCache( Type type )
		{
			typeActions.Remove( type );
		}

		/// <summary>
		/// Clears the record cache for all types. After <see cref="ICsvWriter.WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvWriter.ClearRecordCache()"/> needs to be called to update the
		/// record cache.
		/// </summary>
		public virtual void ClearRecordCache()
		{
			typeActions.Clear();
		}

		/// <summary>
		/// Adds the properties from the mapping. This will recursively
		/// traverse the mapping tree and add all properties for
		/// reference maps.
		/// </summary>
		/// <param name="properties">The properties to be added to.</param>
		/// <param name="mapping">The mapping where the properties are added from.</param>
		protected virtual void AddProperties( CsvPropertyMapCollection properties, CsvClassMap mapping )
		{
			properties.AddRange( mapping.PropertyMaps );
			foreach( var refMap in mapping.ReferenceMaps )
			{
				AddProperties( properties, refMap.Mapping );
			}
		}

		/// <summary>
		/// Creates a parameter for the given property. This will
		/// recursively traverse the mapping to to find property
		/// mapping and create a new property access for each
		/// reference map it goes through.
		/// </summary>
		/// <param name="parameter">The current parameter.</param>
		/// <param name="mapping">The mapping to look for the property map on.</param>
		/// <param name="propertyMap">The property map to look for on the mapping.</param>
		/// <returns>A <see cref="ParameterExpression"/> to access the given property map.</returns>
		protected virtual Expression CreateParameterForProperty( Expression parameter, CsvClassMap mapping, CsvPropertyMap propertyMap )
		{
			var propertyMapping = mapping.PropertyMaps.SingleOrDefault( pm => pm == propertyMap );
			if( propertyMapping != null )
			{
				// If the property map exists on this level of the class
				// mapping, we can return the parameter.
				return parameter;
			}

			// The property isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach( var refMap in mapping.ReferenceMaps )
			{
				var wrappedParameter = Expression.Property( parameter, refMap.Property );
				var param = CreateParameterForProperty( wrappedParameter, refMap.Mapping, propertyMap );
				if( param != null )
				{
					return param;
				}
			}

			return null;
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
					if( writer != null )
					{
						writer.Dispose();
					}
				}

				disposed = true;
				writer = null;
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

#if !NET_2_0
		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <typeparam name="T">The type of the custom class being written.</typeparam>
		/// <returns>The action delegate.</returns>
		protected virtual Action<T> GetWriteRecordAction<T>() where T : class
		{
			var type = typeof( T );
			CreateWriteRecordAction( type );

			return (Action<T>)typeActions[type];
		}

		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <returns>The action delegate.</returns>
		protected virtual Delegate GetWriteRecordAction( Type type )
		{
			CreateWriteRecordAction( type );

			return typeActions[type];
		}

		/// <summary>
		/// Creates the write record action for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <param name="expressionCompiler">The expression compiler.</param>
		protected virtual void CreateWriteRecordAction( Type type )
		{
			if( typeActions.ContainsKey( type ) )
			{
				return;
			}

			var recordParameter = Expression.Parameter( type, "record" );

			if( configuration.Mapping == null )
			{
				// TODO: auto class mapping
				throw new CsvConfigurationException( "No mapping has been created. Use Configuration.ClassMapping to create a map." );
			}

			// Get a list of all the properties so they will
			// be sorted properly.
			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, configuration.Mapping );

			var delegates = new List<Delegate>();

			foreach( var propertyMap in properties )
			{
				if( propertyMap.IgnoreValue )
				{
					// Skip ignored properties.
					continue;
				}

				if( string.IsNullOrEmpty( propertyMap.FormatValue ) && ( propertyMap.TypeConverterValue == null || !propertyMap.TypeConverterValue.CanConvertTo( typeof( string ) ) ) )
				{
					// Skip if the type isn't convertible.
					continue;
				}

				// Find the object that contains this property.
				var currentRecordObject = CreateParameterForProperty( recordParameter, configuration.Mapping, propertyMap );

				Expression fieldExpression = Expression.Property( currentRecordObject, propertyMap.PropertyValue );
				
				if( !string.IsNullOrEmpty( propertyMap.FormatValue ) )
				{
					// Use string.Format instead of TypeConverter.
					var formatExpression = Expression.Constant( propertyMap.FormatValue );
					var method = typeof( string ).GetMethod( "Format", new[] { typeof( IFormatProvider ), typeof( string ), typeof( object[] ) } );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.NewArrayInit( typeof( object ), fieldExpression );
					fieldExpression = Expression.Call( method, Expression.Constant( Configuration.CultureInfo ), formatExpression, fieldExpression );
				}
				else
				{
					var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
					var method = propertyMap.TypeConverterValue.GetType().GetMethod( "ConvertToString", new[] { typeof( CultureInfo ), typeof( object ) } );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, Expression.Constant( Configuration.CultureInfo ), fieldExpression );
				}

				var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
				fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );

				var writeFieldMethodCall = Expression.Call( Expression.Constant( this ), "WriteField", new[] { typeof( string ) }, fieldExpression );

				var actionType = typeof( Action<> ).MakeGenericType( type );
				delegates.Add( Expression.Lambda( actionType, writeFieldMethodCall, recordParameter ).Compile() );
			}

			typeActions[type] = CombineDelegates( delegates );
		}

		/// <summary>
		/// Combines the delegates into a single multicast delegate.
		/// This is needed because Silverlight doesn't have the
		/// Delegate.Combine( params Delegate[] ) overload.
		/// </summary>
		/// <param name="delegates">The delegates to combine.</param>
		/// <returns>A multicast delegate combined from the given delegates.</returns>
		protected virtual Delegate CombineDelegates( IEnumerable<Delegate> delegates )
		{
			return delegates.Aggregate<Delegate, Delegate>( null, Delegate.Combine );
		}
#endif
	}
}
