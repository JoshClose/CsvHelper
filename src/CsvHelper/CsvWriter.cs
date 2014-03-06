// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections;
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
		private ICsvSerializer serializer;
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
		/// Creates a new CSV writer using the given <see cref="TextWriter" />,
		/// a default <see cref="CsvConfiguration"/> and <see cref="CsvSerializer"/>
		/// as the default serializer.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : this( writer, new CsvConfiguration() ) {}

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvConfiguration"/> and <see cref="CsvSerializer"/>
		/// as the default serializer.
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

			this.configuration = configuration;
			serializer = new CsvSerializer( writer, configuration );
		}

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="ICsvSerializer"/>.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		public CsvWriter( ICsvSerializer serializer )
		{
			if( serializer == null )
			{
				throw new ArgumentNullException( "serializer" );
			}

			if( serializer.Configuration == null )
			{
				throw new CsvConfigurationException( "The given serializer has no configuration." );
			}

			this.serializer = serializer;
			configuration = serializer.Configuration;
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
			CheckDisposed();

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
			CheckDisposed();

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

			var typeConverterOptions = TypeConverterOptionsFactory.GetOptions<T>();
			if( typeConverterOptions.CultureInfo == null )
			{
				typeConverterOptions.CultureInfo = configuration.CultureInfo;
			}

			var fieldString = converter.ConvertToString( typeConverterOptions, field );
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
		/// when manually writing records with WriteField.
		/// </summary>
		public virtual void NextRecord()
		{
			CheckDisposed();

			serializer.Write( currentRecord.ToArray() );
			currentRecord.Clear();
		}

#if !NET_2_0
		/// <summary>
		/// Writes the header record from the given properties.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		public virtual void WriteHeader<T>()
		{
			CheckDisposed();

			WriteHeader( typeof( T ) );
		}

		/// <summary>
		/// Writes the header record from the given properties.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		public virtual void WriteHeader( Type type )
		{
			CheckDisposed();

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

			if( configuration.Maps[type] == null )
			{
				configuration.Maps.Add( configuration.AutoMap( type ) );
			}

			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, configuration.Maps[type] );

			foreach( var property in properties )
			{
				if( CanWrite( property ) )
				{
					WriteField( property.Data.Names.FirstOrDefault() );
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
		public virtual void WriteRecord<T>( T record )
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
		[Obsolete( "This method is deprecated and will be removed in the next major release. Use WriteRecords( IEnumerable records ) instead.", false )]
		public virtual void WriteRecords<T>( IEnumerable<T> records )
		{
			CheckDisposed();

			WriteRecords( (IEnumerable)records );
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		/// <param name="records">The list of records to write.</param>
		[Obsolete( "This method is deprecated and will be removed in the next major release. Use WriteRecords( IEnumerable records ) instead.", false )]
		public virtual void WriteRecords( Type type, IEnumerable records )
		{
			CheckDisposed();

			WriteRecords( records );
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords( IEnumerable records )
		{
			CheckDisposed();

			foreach( var record in records )
			{
				if( configuration.HasHeaderRecord && !hasHeaderBeenWritten &&
#if !WINRT_4_5
				!record.GetType().IsPrimitive
#else
				!record.GetType().GetTypeInfo().IsPrimitive
#endif
 )
				{
					WriteHeader( record.GetType() );
				}

				try
				{
					GetWriteRecordAction( record.GetType() ).DynamicInvoke( record );
				}
				catch( Exception ex )
				{
					ExceptionHelper.AddExceptionDataMessage( ex, null, record.GetType(), null, null, null );
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
		public virtual void ClearRecordCache<T>()
		{
			CheckDisposed();

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
			CheckDisposed();

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
			CheckDisposed();

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
		/// Creates a property expression for the given property on the record.
		/// This will recursively traverse the mapping to find the property
		/// and create a safe property accessor for each level as it goes.
		/// </summary>
		/// <param name="recordExpression">The current property expression.</param>
		/// <param name="mapping">The mapping to look for the property to map on.</param>
		/// <param name="propertyMap">The property map to look for on the mapping.</param>
		/// <returns>An Expression to access the given property.</returns>
		protected virtual Expression CreatePropertyExpression( Expression recordExpression, CsvClassMap mapping, CsvPropertyMap propertyMap )
		{
			if( mapping.PropertyMaps.Any( pm => pm == propertyMap ) )
			{
				// The property is on this level.
				return Expression.Property( recordExpression, propertyMap.Data.Property );
			}

			// The property isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach( var refMap in mapping.ReferenceMaps )
			{
				var wrapped = Expression.Property( recordExpression, refMap.Property );
				var propertyExpression = CreatePropertyExpression( wrapped, refMap.Mapping, propertyMap );
				if( propertyExpression == null )
				{
					continue;
				}
				var nullCheckExpression = Expression.Equal( wrapped, Expression.Constant( null ) );

#if !WINRT_4_5
				var isValueType = propertyMap.Data.Property.PropertyType.IsValueType;
#else
				var isValueType = propertyMap.Data.Property.PropertyType.GetTypeInfo().IsValueType;
#endif
				var defaultValueExpression = isValueType
					? (Expression)Expression.New( propertyMap.Data.Property.PropertyType )
					: Expression.Constant( null, propertyMap.Data.Property.PropertyType );
				var conditionExpression = Expression.Condition( nullCheckExpression, defaultValueExpression, propertyExpression );
				return conditionExpression;
			}

			return null;
		}
#endif

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
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
			if( disposed )
			{
				return;
			}

			if( disposing )
			{
				if( serializer != null )
				{
					serializer.Dispose();
				}
			}

			disposed = true;
			serializer = null;
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
		protected virtual Action<T> GetWriteRecordAction<T>()
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
		protected virtual void CreateWriteRecordAction( Type type )
		{
			if( typeActions.ContainsKey( type ) )
			{
				return;
			}

			if( configuration.Maps[type] == null )
			{
				// We need to check again in case the header was not written.
				configuration.Maps.Add( configuration.AutoMap( type ) );
			}

#if !WINRT_4_5
			if( type.IsPrimitive )
#else
			if( type.GetTypeInfo().IsPrimitive )
#endif
			{
				CreateActionForPrimitive( type );
			}
			else
			{
				CreateActionForObject( type );
			}
		}

		/// <summary>
		/// Creates the action for an object.
		/// </summary>
		/// <param name="type">The type of object to create the action for.</param>
		protected virtual void CreateActionForObject( Type type )
		{
			var recordParameter = Expression.Parameter( type, "record" );

			// Get a list of all the properties so they will
			// be sorted properly.
			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, configuration.Maps[type] );

			if( properties.Count == 0 )
			{
				throw new CsvWriterException( string.Format( "No properties are mapped for type '{0}'.", type.FullName ) );
			}

			var delegates = new List<Delegate>();

			foreach( var propertyMap in properties )
			{
				if( !CanWrite( propertyMap ) )
				{
					continue;
				}

				if( propertyMap.Data.TypeConverter == null || !propertyMap.Data.TypeConverter.CanConvertTo( typeof( string ) ) )
				{
					// Skip if the type isn't convertible.
					continue;
				}

				var fieldExpression = CreatePropertyExpression( recordParameter, configuration.Maps[type], propertyMap );

				var typeConverterExpression = Expression.Constant( propertyMap.Data.TypeConverter );
				if( propertyMap.Data.TypeConverterOptions.CultureInfo == null )
				{
					propertyMap.Data.TypeConverterOptions.CultureInfo = configuration.CultureInfo;
				}

				var typeConverterOptions = TypeConverterOptions.Merge( TypeConverterOptionsFactory.GetOptions( propertyMap.Data.Property.PropertyType ), propertyMap.Data.TypeConverterOptions );
				var typeConverterOptionsExpression = Expression.Constant( typeConverterOptions );

				var method = propertyMap.Data.TypeConverter.GetType().GetMethod( "ConvertToString" );
				fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
				fieldExpression = Expression.Call( typeConverterExpression, method, typeConverterOptionsExpression, fieldExpression );

#if !WINRT_4_5
				if( type.IsClass )
#else
				if( type.GetTypeInfo().IsClass )
#endif
				{
					var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
					fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );
				}

				var writeFieldMethodCall = Expression.Call( Expression.Constant( this ), "WriteField", new[] { typeof( string ) }, fieldExpression );

				var actionType = typeof( Action<> ).MakeGenericType( type );
				delegates.Add( Expression.Lambda( actionType, writeFieldMethodCall, recordParameter ).Compile() );
			}

			typeActions[type] = CombineDelegates( delegates );
		}

		/// <summary>
		/// Creates the action for a primitive.
		/// </summary>
		/// <param name="type">The type of primitive to create the action for.</param>
		protected virtual void CreateActionForPrimitive( Type type )
		{
			var recordParameter = Expression.Parameter( type, "record" );

			Expression fieldExpression = Expression.Convert( recordParameter, typeof( object ) );

			var typeConverter = TypeConverterFactory.GetConverter( type );
			var typeConverterExpression = Expression.Constant( typeConverter );
			var method = typeConverter.GetType().GetMethod( "ConvertToString" );

			var typeConverterOptions = TypeConverterOptionsFactory.GetOptions( type );
			if( typeConverterOptions.CultureInfo == null )
			{
				typeConverterOptions.CultureInfo = configuration.CultureInfo;
			}

			fieldExpression = Expression.Call( typeConverterExpression, method, Expression.Constant( typeConverterOptions ), fieldExpression );

			fieldExpression = Expression.Call( Expression.Constant( this ), "WriteField", new[] { typeof( string ) }, fieldExpression );

			var actionType = typeof( Action<> ).MakeGenericType( type );
			typeActions[type] = Expression.Lambda( actionType, fieldExpression, recordParameter ).Compile();
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

		/// <summary>
		/// Checks if the property can be written.
		/// </summary>
		/// <param name="propertyMap">The property map that we are checking.</param>
		/// <returns>A value indicating if the property can be written.
		/// True if the property can be written, otherwise false.</returns>
		protected virtual bool CanWrite( CsvPropertyMap propertyMap )
		{
			var cantWrite =
				// Ignored properties.
				propertyMap.Data.Ignore ||
				// Properties that don't have a public getter
				// and we are honoring the accessor modifier.
				propertyMap.Data.Property.GetGetMethod() == null && !configuration.IgnorePrivateAccessor ||
				// Properties that don't have a getter at all.
				propertyMap.Data.Property.GetGetMethod( true ) == null;
			return !cantWrite;
		}
#endif
	}
}
