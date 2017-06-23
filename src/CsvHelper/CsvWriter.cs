// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Dynamic;
using Microsoft.CSharp.RuntimeBinder;

#pragma warning disable 649
#pragma warning disable 169

namespace CsvHelper
{
	/// <summary>
	/// Used to write CSV files.
	/// </summary>
	public class CsvWriter : IWriter
	{
		private WritingContext context;
		private bool disposed;
		private ISerializer serializer;

		/// <summary>
		/// Gets the writing context.
		/// </summary>
		public virtual WritingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ICsvWriterConfiguration Configuration => context.WriterConfiguration;

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : this( new CsvSerializer( writer, new CsvConfiguration(), false ) ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvWriter( TextWriter writer, bool leaveOpen ) : this( new CsvSerializer( writer, new CsvConfiguration(), leaveOpen ) ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="StreamWriter"/> use to write the CSV file.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvWriter( TextWriter writer, CsvConfiguration configuration ) : this( new CsvSerializer( writer, configuration, false ) ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="ISerializer"/>.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		public CsvWriter( ISerializer serializer )
		{
			this.serializer = serializer ?? throw new ArgumentNullException( nameof( serializer ) );
			if( !( this.serializer.Context is IWriterContext ) )
			{
				throw new InvalidOperationException( "For ICsvSerializer to be used in CsvWriter, ICsvSerializer.Context must also implement IWriterContext." );
			}

			context = serializer.Context;
		}

		/// <summary>
		/// Writes a field that has already been converted to a
		/// <see cref="string"/> from an <see cref="ITypeConverter"/>.
		/// If the field is null, it won't get written. A type converter 
		/// will always return a string, even if field is null. If the 
		/// converter returns a null, it means that the converter has already
		/// written data, and the returned value should not be written.
		/// </summary>
		/// <param name="field">The converted field to write.</param>
		public virtual void WriteConvertedField( string field )
		{
			if( field == null )
			{
				return;
			}

			WriteField( field );
		}

		/// <summary>
		/// Writes the field to the CSV file. The field
		/// may get quotes added to it.
		/// When all fields are written for a record,
		/// <see cref="IWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField( string field )
		{
			var shouldQuote = context.WriterConfiguration.QuoteAllFields;

			if( field != null && context.WriterConfiguration.TrimFields )
			{
				field = field.Trim();
			}

			if( !context.WriterConfiguration.QuoteNoFields && !string.IsNullOrEmpty( field ) )
			{
                if( shouldQuote // Quote all fields
				    || field.Contains( context.WriterConfiguration.QuoteString ) // Contains quote
					|| field[0] == ' ' // Starts with a space
				    || field[field.Length - 1] == ' ' // Ends with a space
				    || field.IndexOfAny( context.WriterConfiguration.QuoteRequiredChars ) > -1 // Contains chars that require quotes
				    || ( context.WriterConfiguration.Delimiter.Length > 0 && field.Contains( context.WriterConfiguration.Delimiter ) ) // Contains delimiter
					|| context.WriterConfiguration.AllowComments && context.Record.Count == 0 && field[0] == context.WriterConfiguration.Comment ) // Comments are on first field starts with comment char
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
		/// <see cref="IWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <param name="field">The field to write.</param>
		/// <param name="shouldQuote">True to quote the field, otherwise false.</param>
		public virtual void WriteField( string field, bool shouldQuote )
		{
            // All quotes must be doubled.       
			if( shouldQuote && !string.IsNullOrEmpty( field ) )
			{
				field = field.Replace( context.WriterConfiguration.QuoteString, context.WriterConfiguration.DoubleQuoteString );
			}

			if( shouldQuote )
			{
				field = context.WriterConfiguration.Quote + field + context.WriterConfiguration.Quote;
			}

			context.Record.Add( field );
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="IWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField<T>( T field )
		{
			var type = field == null ? typeof( string ) : field.GetType();
			var converter = TypeConverterFactory.GetConverter( type );
			WriteField( field, converter );
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="IWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		/// <param name="converter">The converter used to convert the field into a string.</param>
		public virtual void WriteField<T>( T field, ITypeConverter converter )
		{
			var type = field == null ? typeof( string ) : field.GetType();
			context.ReusablePropertyMapData.TypeConverter = converter;
			if( !context.TypeConverterOptionsCache.TryGetValue( type, out TypeConverterOptions typeConverterOptions ) )
			{
				typeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( type ) );
				typeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;
				context.TypeConverterOptionsCache.Add( type, typeConverterOptions );
			}

			context.ReusablePropertyMapData.TypeConverterOptions = typeConverterOptions;

			var fieldString = converter.ConvertToString( field, this, context.ReusablePropertyMapData );
			WriteConvertedField( fieldString );
		}

		/// <summary>
		/// Writes the field to the CSV file
		/// using the given <see cref="ITypeConverter"/>.
		/// When all fields are written for a record,
		/// <see cref="IWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <typeparam name="TConverter">The type of the converter.</typeparam>
		/// <param name="field">The field to write.</param>
		public virtual void WriteField<T, TConverter>( T field )
		{
			var converter = TypeConverterFactory.GetConverter<TConverter>();
			WriteField( field, converter );
		}

	    /// <summary>
	    /// Ends writing of the current record and starts a new record. 
	    /// This needs to be called to serialize the row to the writer.
	    /// </summary>
	    public virtual void NextRecord()
		{
	        try
	        {
	            serializer.Write( context.Record.ToArray() );
				context.Record.Clear();
	            context.Row++;
	        }
	        catch( Exception ex )
	        {
				throw ex as CsvHelperException ?? new CsvWriterException( context, "An unexpected error occurred.", ex );
	        }
		}

	    /// <summary>
	    /// Writes a comment.
	    /// </summary>
	    /// <param name="comment">The comment to write.</param>
	    public virtual void WriteComment( string comment )
	    {
	        WriteField( context.WriterConfiguration.Comment + comment, false );
	    }

        /// <summary>
        /// Writes the header record from the given properties/fields.
        /// </summary>
        /// <typeparam name="T">The type of the record.</typeparam>
        public virtual void WriteHeader<T>()
		{
			WriteHeader( typeof( T ) );
		}

		/// <summary>
		/// Writes the header record from the given properties/fields.
		/// </summary>
		/// <param name="type">The type of the record.</param>
		public virtual void WriteHeader( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException( nameof( type ) );
			}

			if( !context.WriterConfiguration.HasHeaderRecord )
			{
				throw new CsvWriterException( context, "Configuration.HasHeaderRecord is false. This will need to be enabled to write the header." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new CsvWriterException( context, "The header record has already been written. You can't write it more than once." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new CsvWriterException( context, "Records have already been written. You can't write the header after writing records has started." );
			}

			if( type == typeof( object ) )
			{
				return;
			}

			if( context.WriterConfiguration.Maps[type] == null )
			{
				context.WriterConfiguration.Maps.Add( context.WriterConfiguration.AutoMap( type ) );
			}

			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, context.WriterConfiguration.Maps[type] );

			foreach( var property in properties )
			{
				if( CanWrite( property ) )
				{
					if( property.Data.IndexEnd >= property.Data.Index )
					{
						var count = property.Data.IndexEnd - property.Data.Index + 1;
						for( var i = 1; i <= count; i++ )
						{
							WriteField( property.Data.Names.FirstOrDefault() + i );
						}
					}
					else
					{
						WriteField( property.Data.Names.FirstOrDefault() );
					}
				}
			}

			context.HasHeaderBeenWritten = true;
		}

		/// <summary>
		/// Writes the header record for the given dynamic object.
		/// </summary>
		/// <param name="record">The dynamic record to write.</param>
		public virtual void WriteDynamicHeader( IDynamicMetaObjectProvider record )
		{
			if( record == null )
			{
				throw new ArgumentNullException( nameof( record ) );
			}

			if( !context.WriterConfiguration.HasHeaderRecord )
			{
				throw new CsvWriterException( context, "Configuration.HasHeaderRecord is false. This will need to be enabled to write the header." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new CsvWriterException( context, "The header record has already been written. You can't write it more than once." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new CsvWriterException( context, "Records have already been written. You can't write the header after writing records has started." );
			}

			var metaObject = record.GetMetaObject( Expression.Constant( record ) );
			var names = metaObject.GetDynamicMemberNames();
			foreach( var name in names )
			{
				WriteField( name );
			}

			context.HasHeaderBeenWritten = true;
		}

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		public virtual void WriteRecord<T>( T record )
		{
			var dynamicRecord = record as IDynamicMetaObjectProvider;
			if( dynamicRecord != null )
			{
				if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten )
				{
					WriteDynamicHeader( dynamicRecord );
				    NextRecord();
				}
			}

			try
			{
				GetWriteRecordAction( record ).DynamicInvoke( record );
				context.HasHeaderBeenWritten = true;
            }
            catch( Exception ex )
			{
				throw ex as CsvHelperException ?? new CsvWriterException( context, "An unexpected error occurred.", ex );
			}
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords( IEnumerable records )
		{
			Type recordType = null;
			try
			{
				// Write the header. If records is a List<dynamic>, the header won't be written.
				// This is because typeof( T ) = Object.
				var genericEnumerable = records.GetType().GetInterfaces().FirstOrDefault( t => t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == typeof( IEnumerable<> ) );
				if( genericEnumerable != null )
				{
					recordType = genericEnumerable.GetGenericArguments().Single();
					var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
					if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten && !isPrimitive && recordType != typeof( object ) )
					{
						WriteHeader( recordType );
                        if( context.HasHeaderBeenWritten )
                        {
                            NextRecord();
                        }
					}
				}

				foreach( var record in records )
				{
					recordType = record.GetType();

					var dynamicObject = record as IDynamicMetaObjectProvider;
					if( dynamicObject != null )
					{
						if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten )
						{
							WriteDynamicHeader( dynamicObject );
                            NextRecord();
						}
					}
					else
					{
						// If records is a List<dynamic>, the header hasn't been written yet.
						// Write the header based on the record type.
						var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten && !isPrimitive )
						{
							WriteHeader( recordType );
                            NextRecord();
						}
					}

                    try
					{
						GetWriteRecordAction( record ).DynamicInvoke( record );
                    }
                    catch( TargetInvocationException ex )
					{
						throw ex.InnerException;
					}

					NextRecord();
				}
			}
			catch( Exception ex )
			{
				throw ex as CsvHelperException ?? new CsvWriterException( context, "An unexpected error occurred.", ex );
			}
		}

		/// <summary>
		/// Adds the properties/fields from the mapping. This will recursively
		/// traverse the mapping tree and add all properties/fields for
		/// reference maps.
		/// </summary>
		/// <param name="properties">The properties/fields to be added to.</param>
		/// <param name="mapping">The mapping where the properties/fields are added from.</param>
		protected virtual void AddProperties( CsvPropertyMapCollection properties, CsvClassMap mapping )
		{
			properties.AddRange( mapping.PropertyMaps );
			foreach( var refMap in mapping.ReferenceMaps )
			{
				AddProperties( properties, refMap.Data.Mapping );
			}
		}

		/// <summary>
		/// Creates a property/field expression for the given property on the record.
		/// This will recursively traverse the mapping to find the property/field
		/// and create a safe property/field accessor for each level as it goes.
		/// </summary>
		/// <param name="recordExpression">The current property/field expression.</param>
		/// <param name="mapping">The mapping to look for the property/field to map on.</param>
		/// <param name="propertyMap">The property/field map to look for on the mapping.</param>
		/// <returns>An Expression to access the given property/field.</returns>
		protected virtual Expression CreatePropertyExpression( Expression recordExpression, CsvClassMap mapping, CsvPropertyMap propertyMap )
		{
			if( mapping.PropertyMaps.Any( pm => pm == propertyMap ) )
			{
				// The property/field is on this level.
				if( propertyMap.Data.Member is PropertyInfo )
				{
					return Expression.Property( recordExpression, (PropertyInfo)propertyMap.Data.Member );
				}

				if( propertyMap.Data.Member is FieldInfo )
				{
					return Expression.Field( recordExpression, (FieldInfo)propertyMap.Data.Member );
				}
			}

			// The property/field isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach( var refMap in mapping.ReferenceMaps )
			{
				var wrapped = refMap.Data.Member.GetMemberExpression( recordExpression );
				var propertyExpression = CreatePropertyExpression( wrapped, refMap.Data.Mapping, propertyMap );
				if( propertyExpression == null )
				{
					continue;
				}

				if( refMap.Data.Member.MemberType().GetTypeInfo().IsValueType )
				{
					return propertyExpression;
				}

				var nullCheckExpression = Expression.Equal( wrapped, Expression.Constant( null ) );

				var isValueType = propertyMap.Data.Member.MemberType().GetTypeInfo().IsValueType;
				var isGenericType = isValueType && propertyMap.Data.Member.MemberType().GetTypeInfo().IsGenericType;
				Type propertyType;
				if( isValueType && !isGenericType && !context.WriterConfiguration.UseNewObjectForNullReferenceMembers )
				{
					propertyType = typeof( Nullable<> ).MakeGenericType( propertyMap.Data.Member.MemberType() );
					propertyExpression = Expression.Convert( propertyExpression, propertyType );
				}
				else
				{
					propertyType = propertyMap.Data.Member.MemberType();
				}

				var defaultValueExpression = isValueType && !isGenericType
					? (Expression)Expression.New( propertyType )
					: Expression.Constant( null, propertyType );
				var conditionExpression = Expression.Condition( nullCheckExpression, defaultValueExpression, propertyExpression );
				return conditionExpression;
			}

			return null;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose( !context.LeaveOpen );
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
				serializer?.Dispose();
			}

			serializer = null;
			context = null;
			disposed = true;
		}

		/// <summary>
		/// Gets the action delegate used to write the custom
		/// class object to the writer.
		/// </summary>
		/// <typeparam name="T">The type of the custom class being written.</typeparam>
		/// <param name="record"></param>
		/// <returns>The action delegate.</returns>
		protected virtual Delegate GetWriteRecordAction<T>( T record )
		{
			var type = typeof( T );
			if( type == typeof( object ) )
			{
				type = record.GetType();
			}

			Delegate action;
			if( !context.TypeActions.TryGetValue( type, out action ) )
			{
				action = CreateWriteRecordAction( type, record );
			}

			return action;
		}

		/// <summary>
		/// Creates the write record action for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <param name="record">The record that will be written.</param>
		protected virtual Delegate CreateWriteRecordAction<T>( Type type, T record )
		{
			var expandoObject = record as ExpandoObject;
			if( expandoObject != null )
			{
				return CreateActionForExpandoObject( expandoObject );
			}
			
			var dynamicObject = record as IDynamicMetaObjectProvider;
			if( dynamicObject != null )
			{
				return CreateActionForDynamic( dynamicObject );
			}

			if( context.WriterConfiguration.Maps[type] == null )
			{
				// We need to check again in case the header was not written.
				context.WriterConfiguration.Maps.Add( context.WriterConfiguration.AutoMap( type ) );
			}

			if( type.GetTypeInfo().IsPrimitive )
			{
				return CreateActionForPrimitive( type );
			}

			return CreateActionForObject( type );
		}

		/// <summary>
		/// Creates the action for an object.
		/// </summary>
		/// <param name="type">The type of object to create the action for.</param>
		protected virtual Delegate CreateActionForObject( Type type )
		{
			var recordParameter = Expression.Parameter( type, "record" );

			// Get a list of all the properties/fields so they will
			// be sorted properly.
			var properties = new CsvPropertyMapCollection();
			AddProperties( properties, context.WriterConfiguration.Maps[type] );

			if( properties.Count == 0 )
			{
				throw new CsvWriterException( context, $"No properties are mapped for type '{type.FullName}'." );
			}

			var delegates = new List<Delegate>();

			foreach( var propertyMap in properties )
			{
				if( propertyMap.Data.WritingConvertExpression != null )
				{
					// The user is providing the expression to do the conversion.
					Expression exp = Expression.Invoke( propertyMap.Data.WritingConvertExpression, recordParameter );
					exp = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, exp );
					delegates.Add( Expression.Lambda( typeof( Action<> ).MakeGenericType( type ), exp, recordParameter ).Compile() );
					continue;
				}

				if( !CanWrite( propertyMap ) )
				{
					continue;
				}

				Expression fieldExpression;

				if( propertyMap.Data.IsConstantSet )
				{
					fieldExpression = Expression.Constant( propertyMap.Data.Constant );
				}
				else
				{
					if( propertyMap.Data.TypeConverter == null )
					{
						// Skip if the type isn't convertible.
						continue;
					}

					fieldExpression = CreatePropertyExpression( recordParameter, context.WriterConfiguration.Maps[type], propertyMap );

					var typeConverterExpression = Expression.Constant( propertyMap.Data.TypeConverter );
					propertyMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( propertyMap.Data.Member.MemberType() ), propertyMap.Data.TypeConverterOptions );
					propertyMap.Data.TypeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;

					var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( this ), Expression.Constant( propertyMap.Data ) );

					if( type.GetTypeInfo().IsClass )
					{
						var areEqualExpression = Expression.Equal( recordParameter, Expression.Constant( null ) );
						fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );
					}
				}

				var writeFieldMethodCall = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, fieldExpression );

				var actionType = typeof( Action<> ).MakeGenericType( type );
				delegates.Add( Expression.Lambda( actionType, writeFieldMethodCall, recordParameter ).Compile() );
			}

			var action = CombineDelegates( delegates );
			context.TypeActions[type] = action;

			return action;
		}

		/// <summary>
		/// Creates the action for a primitive.
		/// </summary>
		/// <param name="type">The type of primitive to create the action for.</param>
		protected virtual Delegate CreateActionForPrimitive( Type type )
		{
			var recordParameter = Expression.Parameter( type, "record" );

			Expression fieldExpression = Expression.Convert( recordParameter, typeof( object ) );

			var typeConverter = TypeConverterFactory.GetConverter( type );
			var typeConverterExpression = Expression.Constant( typeConverter );
			var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );

			var propertyMapData = new CsvPropertyMapData( null )
			{
				Index = 0,
				TypeConverter = typeConverter,
				TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( type ) )
			};
			propertyMapData.TypeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;

			fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( this ), Expression.Constant( propertyMapData ) );
			fieldExpression = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, fieldExpression );

			var actionType = typeof( Action<> ).MakeGenericType( type );
			var action = Expression.Lambda( actionType, fieldExpression, recordParameter ).Compile();
			context.TypeActions[type] = action;

			return action;
		}

		/// <summary>
		/// Creates an action for an ExpandoObject. This needs to be separate
		/// from other dynamic objects due to what seems to be an issue in ExpandoObject
		/// where expandos with the same properties/fields sometimes test as not equal.
		/// </summary>
		/// <param name="obj">The ExpandoObject.</param>
		/// <returns></returns>
		protected virtual Delegate CreateActionForExpandoObject( ExpandoObject obj )
		{
			Action<object> action = record =>
			{
				var dict = (IDictionary<string, object>)record;
				foreach( var val in dict.Values )
				{
					WriteField( val );
				}
			};

			context.TypeActions[typeof( ExpandoObject )] = action;

			return action;
		}

		/// <summary>
		/// Creates the action for a dynamic object.
		/// </summary>
		/// <param name="provider">The dynamic object.</param>
		protected virtual Delegate CreateActionForDynamic( IDynamicMetaObjectProvider provider )
		{
			// http://stackoverflow.com/a/14011692/68499

			var type = provider.GetType();
			var parameterExpression = Expression.Parameter( typeof( object ), "record" );

			var metaObject = provider.GetMetaObject( parameterExpression );
			var propertyNames = metaObject.GetDynamicMemberNames();

			var delegates = new List<Delegate>();
			foreach( var propertyName in propertyNames )
			{
				var getMemberBinder = (GetMemberBinder)Binder.GetMember( 0, propertyName, type, new[] { CSharpArgumentInfo.Create( 0, null ) } );
				var getMemberMetaObject = metaObject.BindGetMember( getMemberBinder );
				var fieldExpression = getMemberMetaObject.Expression;
				fieldExpression = Expression.Call( Expression.Constant( this ), nameof( WriteField ), new[] { typeof( object ) }, fieldExpression );
				fieldExpression = Expression.Block( fieldExpression, Expression.Label( CallSiteBinder.UpdateLabel ) );
				var lambda = Expression.Lambda( fieldExpression, parameterExpression );
				delegates.Add( lambda.Compile() );
			}

			var action = CombineDelegates( delegates );

			context.TypeActions[type] = action;

			return action;
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
		/// Checks if the property/field can be written.
		/// </summary>
		/// <param name="propertyMap">The property/field map that we are checking.</param>
		/// <returns>A value indicating if the property/field can be written.
		/// True if the property/field can be written, otherwise false.</returns>
		protected virtual bool CanWrite( CsvPropertyMap propertyMap )
		{
			var cantWrite =
				// Ignored properties/fields.
				propertyMap.Data.Ignore;

			var property = propertyMap.Data.Member as PropertyInfo;
			if( property != null )
			{
				cantWrite = cantWrite ||
				// Properties that don't have a public getter
				// and we are honoring the accessor modifier.
				property.GetGetMethod() == null && !context.WriterConfiguration.IncludePrivateMembers ||
				// Properties that don't have a getter at all.
				property.GetGetMethod( true ) == null;
			}

			return !cantWrite;
		}
	}
}
