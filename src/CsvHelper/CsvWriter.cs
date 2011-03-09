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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
		private bool hasHeaderBeenWritten;
        private readonly Dictionary<Type, CsvPropertyInfo[]> typeProperties = new Dictionary<Type, CsvPropertyInfo[]>();
		private readonly Dictionary<Type, Delegate> typeActions = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets or sets the delimiter used to
		/// separate the fields of the CSV records.
		/// </summary>
		public virtual char Delimiter { get; private set; }

		/// <summary>
		/// Gets are sets a value indicating if the
		/// CSV file has a header record.
		/// </summary>
		public virtual bool HasHeaderRecord { get; private set; }

		/// <summary>
		/// Gets the binding flags used to get the properties
		/// from the the custom class object.
		/// </summary>
		public virtual BindingFlags PropertyBindingFlags { get; private set; }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="StreamWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : this( writer, new CsvWriterOptions() ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="StreamWriter"/>
		/// and <see cref="CsvWriterOptions"/>.
		/// </summary>
		/// <param name="writer">The <see cref="StreamWriter"/> use to write the CSV file.</param>
		/// <param name="options">The <see cref="CsvWriterOptions"/> used to write the CSV file.</param>
		public CsvWriter( TextWriter writer, CsvWriterOptions options )
		{
			this.writer = writer;
			Delimiter = options.Delimiter;
			HasHeaderRecord = options.HasHeaderRecord;
			PropertyBindingFlags = options.PropertyBindingFlags;
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField( string field )
		{
			if( !string.IsNullOrEmpty( field ) )
			{
				var hasQuote = false;
				if( field.Contains( "\"" ) )
				{
					// All quotes must be doubled.
					field = field.Replace( "\"", "\"\"" );
					hasQuote = true;
				}
				if( hasQuote ||
					field[0] == ' ' ||
					field[field.Length - 1] == ' ' ||
					field.Contains( Delimiter.ToString() ) ||
					field.Contains( "\n" ) )
				{
					// Surround the field in double quotes.
					field = string.Format( "\"{0}\"", field );
				}
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
			else if( type.IsValueType )
			{
				WriteField( field.ToString() );
			}
			else
			{
				var converter = TypeDescriptor.GetConverter( typeof( T ) );
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
		public virtual void WriteField<T>( T field, TypeConverter converter )
		{
			CheckDisposed();

			var fieldString = converter.ConvertToString( field );
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

			var record = string.Join( Delimiter.ToString(), currentRecord.ToArray() );
			writer.WriteLine( record );
			writer.Flush();
			currentRecord.Clear();
		}

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		public virtual void WriteRecord<T>( T record )
		{
			CheckDisposed();

			if( HasHeaderRecord && !hasHeaderBeenWritten )
			{
				WriteHeader( GetApplicableProperties<T>() );
			}

			GetAction<T>()( this, record );

			NextRecord();
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords<T>( IEnumerable<T> records )
		{
			CheckDisposed();

			if( HasHeaderRecord && !hasHeaderBeenWritten )
			{
				WriteHeader( GetApplicableProperties<T>() );
			}

			foreach( var record in records )
			{
				GetAction<T>()( this, record );
				NextRecord();
			}
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

		/// <summary>
		/// Writes the header record from the given properties.
		/// </summary>
		/// <param name="properties">The properties to write the header record from.</param>
        protected virtual void WriteHeader(CsvPropertyInfo[] properties)
		{
			foreach( var property in properties )
			{
                if (!property.Ignore)
				{
                    WriteField(property.Name);
				}
			}
			NextRecord();
			hasHeaderBeenWritten = true;
		}

		/// <summary>
		/// Gets the properties for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type to get the properties for.</typeparam>
		/// <returns>The properties for the given <see cref="Type"/>/</returns>
		private CsvPropertyInfo[] GetApplicableProperties<T>()
		{
			var type = typeof( T );
			if( !typeProperties.ContainsKey( type ) )
			{
			    var propertiesInfo = type
			        .GetProperties(PropertyBindingFlags)
                    .Select(p => new CsvPropertyInfo(p))
			        .Where(i => !i.Ignore)                 			        
			        .ToArray();

                // Only sort if there is at least one attribute
                // that has the field index specified.			
				var shouldSort = propertiesInfo.Any(i => i.HasFieldIndex);
			    
				if( shouldSort )
				{
                    Array.Sort(propertiesInfo, new CsvPropertyInfoComparer(false));                    
				}
				
                typeProperties[type] = propertiesInfo;   
			}
			return typeProperties[type];
		}

		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <typeparam name="T">The type of the custom class being written.</typeparam>
		/// <returns>The action delegate.</returns>
		protected virtual Action<CsvWriter, T> GetAction<T>()
		{
			var type = typeof( T );

			if( !typeActions.ContainsKey( type ) )
			{
				var properties = GetApplicableProperties<T>();

                if (properties.Length == 0)
                {
                    throw new CsvHelperException("Type " + type.Name + " does not have properties to write to CSV files.");
                }

				Action<CsvWriter, T> func = null;
				var writerParameter = Expression.Parameter( typeof( CsvWriter ), "writer" );
				var recordParameter = Expression.Parameter( typeof( T ), "record" );
				foreach( var info in properties )
				{                   
				    var property = info.Property;
				    var propertyType = property.PropertyType;
                    var typeConverter = info.FindTypeConverter();					

					Expression fieldExpression = Expression.Property( recordParameter, property );
					if( typeConverter != null && typeConverter.CanConvertTo( typeof( string ) ) )
					{
						// Convert the property value to a string using the
						// TypeConverter specified in the TypeConverterAttribute.                        
						var typeConverterExpression = Expression.Constant( typeConverter );
						var method = typeConverter.GetType().GetMethod( "ConvertToInvariantString", new[] { typeof( object ) } );
						fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
						fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression );
					}
                    else if (propertyType != typeof(string))
					{
                        if (propertyType.IsValueType)
						{
							// Convert the property value to a string using ToString.
							var formatProvider = Expression.Constant( CultureInfo.InvariantCulture, typeof( IFormatProvider ) );
							var method = property.PropertyType.GetMethod( "ToString", new[] { typeof( IFormatProvider ) } );
							fieldExpression = method != null ? Expression.Call( fieldExpression, method, formatProvider ) : Expression.Call( fieldExpression, "ToString", null, null );
						}
						else
						{
							// Convert the property value to a string using
							// the default TypeConverter for the properties type.
                            typeConverter = TypeDescriptor.GetConverter(propertyType);
							if( !typeConverter.CanConvertTo( typeof( string ) ) )
							{
								continue;
							}
							var method = typeConverter.GetType().GetMethod( "ConvertToInvariantString", new[] { typeof( object ) } );
							var typeConverterExpression = Expression.Constant( typeConverter );
							fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
							fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression );
						}
					}

					var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
					fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );

					var body = Expression.Call( writerParameter, "WriteField", new[] { typeof( string ) }, fieldExpression );
					func += Expression.Lambda<Action<CsvWriter, T>>( body, writerParameter, recordParameter ).Compile();
				}
                
                // If func is null we will store just empty writer
                typeActions[type] = func ?? ((w, rec) => { });                
			}

			return (Action<CsvWriter, T>)typeActions[type];
		}
	}
}
