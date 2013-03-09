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
				var converter = TypeConverterFactory.CreateTypeConverter( typeof( T ) );
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

			var fieldString = Configuration.UseInvariantCulture
			                  	? converter.ConvertToString( CultureInfo.InvariantCulture, field )
			                  	: converter.ConvertToString( field );
			WriteField( fieldString );
		}

		/// <summary>
		/// Ends writing of the current record
		/// and starts a new record. This is used
		/// when manually writing records with <see cref="ICsvWriter.WriteField{T}" />
		/// </summary>
		public virtual void NextRecord()
		{
			CheckDisposed();

			var record = string.Join( configuration.Delimiter.ToString(), currentRecord.ToArray() );
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

			if( configuration.Properties.Count == 0 )
			{
				configuration.AttributeMapping( type );
			}

			var properties = new CsvPropertyMapCollection();
			properties.AddRange( configuration.Properties );
			foreach( var reference in configuration.References )
			{
				properties.AddRange( reference.ReferenceProperties );
			}

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

			GetWriteRecordAction<T>()( this, record );

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

			GetWriteRecordAction( type ).DynamicInvoke( this, record );

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
				GetWriteRecordAction<T>()( this, record );
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
				GetWriteRecordAction( type ).DynamicInvoke( this, record );
				NextRecord();
			}
		}

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="ICsvWriter.WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvWriter.InvalidateRecordCache{T}"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		public virtual void InvalidateRecordCache<T>() where T : class
		{
			InvalidateRecordCache( typeof( T ) );
		}

		/// <summary>
		/// Invalidates the record cache for the given type. After <see cref="ICsvWriter.WriteRecord{T}"/> is called the
		/// first time, code is dynamically generated based on the <see cref="CsvPropertyMapCollection"/>,
		/// compiled, and stored for the given type T. If the <see cref="CsvPropertyMapCollection"/>
		/// changes, <see cref="ICsvWriter.InvalidateRecordCache"/> needs to be called to updated the
		/// record cache.
		/// </summary>
		public virtual void InvalidateRecordCache( Type type )
		{
			typeActions.Remove( type );
			configuration.Properties.Clear();
			hasHeaderBeenWritten = false;
			hasRecordBeenWritten = false;
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

#if NET_3_5
		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <typeparam name="T">The type of the custom class being written.</typeparam>
		/// <returns>The action delegate.</returns>
		protected virtual Action<ICsvWriter, T> GetWriteRecordAction<T>() where T : class
		{
			var type = typeof( T );

			if( !typeActions.ContainsKey( type ) )
			{
				Action<ICsvWriter, T> func = null;
				var writerParameter = Expression.Parameter( typeof( ICsvWriter ), "writer" );
				var recordParameter = Expression.Parameter( typeof( T ), "record" );

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

					if( propertyMap.TypeConverterValue == null )
					{
						// Skip if the type isn't convertible.
						continue;
					}

					Expression fieldExpression = Expression.Property( recordParameter, propertyMap.PropertyValue );
					var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
					var convertMethod = Configuration.UseInvariantCulture ? "ConvertToInvariantString" : "ConvertToString";
					var method = propertyMap.TypeConverterValue.GetType().GetMethod( convertMethod, new[] { typeof( object ) } );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression );

					var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
					fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );

					var body = Expression.Call( writerParameter, "WriteField", new[] { typeof( string ) }, fieldExpression );
					func += Expression.Lambda<Action<ICsvWriter, T>>( body, writerParameter, recordParameter ).Compile();
				}

				typeActions[type] = func;
			}

			return (Action<ICsvWriter, T>)typeActions[type];
		}

		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <returns>The action delegate.</returns>
		protected virtual Delegate GetWriteRecordAction( Type type )
		{
			if( !typeActions.ContainsKey( type ) )
			{
				var delegates = new List<Delegate>();
				var writerParameter = Expression.Parameter( typeof( ICsvWriter ), "writer" );
				var recordParameter = Expression.Parameter( type, "record" );

				if( configuration.Properties.Count == 0 )
				{
					configuration.AttributeMapping( type );
				}

				foreach( var propertyMap in configuration.Properties )
				{
					if( propertyMap.IgnoreValue )
					{
						// Skip ignored properties.
						continue;
					}

					if( propertyMap.TypeConverterValue == null )
					{
						// Skip if the type isn't convertible.
						continue;
					}

					Expression fieldExpression = Expression.Property( recordParameter, propertyMap.PropertyValue );
					var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
					var convertMethod = Configuration.UseInvariantCulture ? "ConvertToInvariantString" : "ConvertToString";
					var method = propertyMap.TypeConverterValue.GetType().GetMethod( convertMethod, new[] { typeof( object ) } );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression );

					var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
					fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );

					var body = Expression.Call( writerParameter, "WriteField", new[] { typeof( string ) }, fieldExpression );
					var actionType = typeof( Action<,> ).MakeGenericType( typeof( ICsvWriter ), type );
					delegates.Add( Expression.Lambda( actionType, body, writerParameter, recordParameter ).Compile() );
				}

				typeActions[type] = Delegate.Combine( delegates.ToArray() );
			}

			return typeActions[type];
		}
#elif !NET_2_0
		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <typeparam name="T">The type of the custom class being written.</typeparam>
		/// <returns>The action delegate.</returns>
		protected virtual Action<ICsvWriter, T> GetWriteRecordAction<T>() where T : class
		{
			var type = typeof( T );
			CreateWriteRecordAction( type, ( body, writerParameter, recordParameter ) => Expression.Lambda<Action<ICsvWriter, T>>( body, writerParameter, recordParameter ).Compile() );

			return (Action<ICsvWriter, T>)typeActions[type];
		}

		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <returns>The action delegate.</returns>
		protected virtual Action<ICsvWriter, object> GetWriteRecordAction( Type type )
		{
			CreateWriteRecordAction( type, ( body, writerParameter, recordParameter ) => Expression.Lambda<Action<ICsvWriter, object>>( body, writerParameter, recordParameter ).Compile() );

			return (Action<ICsvWriter, object>)typeActions[type];
		}

		/// <summary>
		/// Creates the write record action for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <param name="expressionCompiler">The expression compiler.</param>
		protected virtual void CreateWriteRecordAction( Type type, Func<Expression, ParameterExpression, ParameterExpression, Delegate> expressionCompiler )
		{
			if( typeActions.ContainsKey( type ) )
			{
				return;
			}

			var writerParameter = Expression.Parameter( typeof( ICsvWriter ), "writer" );
			var recordParameter = Expression.Parameter( type, "record" );

			if( configuration.Properties.Count == 0 )
			{
				configuration.AttributeMapping( type );
			}

			// Get a list of all the properties so they will
			// be sorted properly.
			var properties = new CsvPropertyMapCollection();
			properties.AddRange( configuration.Properties );
			foreach( var reference in configuration.References )
			{
				properties.AddRange( reference.ReferenceProperties );
			}

			// A list of expressions that will go inside
			// the lambda code block.
			var expressions = new List<Expression>();

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

				// So we don't have to access a modified closure.
				var propertyMapCopy = propertyMap;

				// Get the reference this property is a part of.
				var reference = ( from r in configuration.References
				                  from p in r.ReferenceProperties
				                  where p == propertyMapCopy
				                  select r ).SingleOrDefault();

				// Get the current object. Either the param passed in
				// or a reference property object.
				var currentRecordObject =
					reference == null
						? (Expression)recordParameter
						: Expression.Property( recordParameter, reference.Property );

				Expression fieldExpression = Expression.Property( currentRecordObject, propertyMap.PropertyValue );
				
				if( !string.IsNullOrEmpty( propertyMap.FormatValue ) )
				{
					// Use string.Format instead of TypeConverter.
					var formatExpression = Expression.Constant( propertyMap.FormatValue );
					//MethodInfo method;
					if( configuration.UseInvariantCulture )
					{
						var method = typeof( string ).GetMethod( "Format", new[] { typeof( IFormatProvider ), typeof( string ), typeof( object ) } );
						fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
						fieldExpression = Expression.Call( method, Expression.Constant( CultureInfo.InvariantCulture ), formatExpression, fieldExpression );
					}
					else
					{
						var method = typeof( string ).GetMethod( "Format", new[] { typeof( string ), typeof( object ) } );
						fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
						fieldExpression = Expression.Call( method, formatExpression, fieldExpression );
					}
				}
				else
				{
					var typeConverterExpression = Expression.Constant( propertyMap.TypeConverterValue );
					var convertMethod = Configuration.UseInvariantCulture ? "ConvertToInvariantString" : "ConvertToString";
					var method = propertyMap.TypeConverterValue.GetType().GetMethod( convertMethod, new[] { typeof( object ) } );

					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );

					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression );
				}

				var areEqualExpression = Expression.Equal( currentRecordObject, Expression.Constant( null ) );
				fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );

				var writeFieldMethodCall = Expression.Call( writerParameter, "WriteField", new[] { typeof( string ) }, fieldExpression );
				expressions.Add( writeFieldMethodCall );
			}

			var body = Expression.Block( expressions );
			var func = expressionCompiler( body, writerParameter, recordParameter );

			typeActions[type] = func;
		}
#endif
	}
}
