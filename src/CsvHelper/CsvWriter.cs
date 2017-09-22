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
using System.Threading.Tasks;

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
		public virtual IWriterConfiguration Configuration => context.WriterConfiguration;

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : this( new CsvSerializer( writer, new Configuration.Configuration(), false ) ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvWriter( TextWriter writer, bool leaveOpen ) : this( new CsvSerializer( writer, new Configuration.Configuration(), leaveOpen ) ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="StreamWriter"/> use to write the CSV file.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvWriter( TextWriter writer, Configuration.Configuration configuration ) : this( new CsvSerializer( writer, configuration, false ) ) { }

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

			if( field != null && ( context.WriterConfiguration.TrimOptions & TrimOptions.Trim ) == TrimOptions.Trim )
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
		/// <see cref="CsvHelper.Configuration.Configuration.QuoteAllFields"/>
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
			var converter = Configuration.TypeConverterFactory.GetConverter( type );
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
			context.ReusableMemberMapData.TypeConverter = converter;
			if( !context.TypeConverterOptionsCache.TryGetValue( type, out TypeConverterOptions typeConverterOptions ) )
			{
				typeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( type ) );
				typeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;
				context.TypeConverterOptionsCache.Add( type, typeConverterOptions );
			}

			context.ReusableMemberMapData.TypeConverterOptions = typeConverterOptions;

			var fieldString = converter.ConvertToString( field, this, context.ReusableMemberMapData );
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
			var converter = Configuration.TypeConverterFactory.GetConverter<TConverter>();
			WriteField( field, converter );
		}

		/// <summary>
		/// Serializes the row to the <see cref="TextWriter"/>.
		/// </summary>
		public virtual void Flush()
		{
			// Don't forget about the async method below!

			serializer.Write( context.Record.ToArray() );
			context.Record.Clear();
		}

		/// <summary>
		/// Serializes the row to the <see cref="TextWriter"/>.
		/// </summary>
		public virtual async Task FlushAsync()
		{
			await serializer.WriteAsync( context.Record.ToArray() );
			context.Record.Clear();
		}

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This automatically flushes the writer.
		/// </summary>
		public virtual void NextRecord()
		{
			// Don't forget about the async method below!

			try
			{
				Flush();
				serializer.WriteLine();
	            context.Row++;
	        }
	        catch( Exception ex )
	        {
				throw ex as CsvHelperException ?? new WriterException( context, "An unexpected error occurred.", ex );
	        }
		}

		/// <summary>
		/// Ends writing of the current record and starts a new record.
		/// This automatically flushes the writer.
		/// </summary>
		public virtual async Task NextRecordAsync()
		{
			try
			{
				await FlushAsync();
				await serializer.WriteLineAsync();
				context.Row++;
			}
			catch( Exception ex )
			{
				throw ex as CsvHelperException ?? new WriterException( context, "An unexpected error occurred.", ex );
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
        /// Writes the header record from the given members.
        /// </summary>
        /// <typeparam name="T">The type of the record.</typeparam>
        public virtual void WriteHeader<T>()
		{
			WriteHeader( typeof( T ) );
		}

		/// <summary>
		/// Writes the header record from the given members.
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
				throw new WriterException( context, "Configuration.HasHeaderRecord is false. This will need to be enabled to write the header." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new WriterException( context, "The header record has already been written. You can't write it more than once." );
			}

			if( context.HasRecordBeenWritten )
			{
				throw new WriterException( context, "Records have already been written. You can't write the header after writing records has started." );
			}

			if( type == typeof( object ) )
			{
				return;
			}

			if( context.WriterConfiguration.Maps[type] == null )
			{
				context.WriterConfiguration.Maps.Add( context.WriterConfiguration.AutoMap( type ) );
			}

			var members = new MemberMapCollection();
			AddMembers( members, context.WriterConfiguration.Maps[type] );

			foreach( var member in members )
			{
				if( CanWrite( member ) )
				{
					if( member.Data.IndexEnd >= member.Data.Index )
					{
						var count = member.Data.IndexEnd - member.Data.Index + 1;
						for( var i = 1; i <= count; i++ )
						{
							WriteField( member.Data.Names.FirstOrDefault() + i );
						}
					}
					else
					{
						WriteField( member.Data.Names.FirstOrDefault() );
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
				throw new WriterException( context, "Configuration.HasHeaderRecord is false. This will need to be enabled to write the header." );
			}

			if( context.HasHeaderBeenWritten )
			{
				throw new WriterException( context, "The header record has already been written. You can't write it more than once." );
			}

			if( context.HasRecordBeenWritten )
			{
				throw new WriterException( context, "Records have already been written. You can't write the header after writing records has started." );
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
			if( record is IDynamicMetaObjectProvider dynamicRecord )
			{
				if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten )
				{
					WriteDynamicHeader( dynamicRecord );
					NextRecord();
				}
			}

			try
			{
				GetWriteRecordAction( record )( record );
				context.HasHeaderBeenWritten = true;
            }
            catch( Exception ex )
			{
				throw ex as CsvHelperException ?? new WriterException( context, "An unexpected error occurred.", ex );
			}
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords<T>( IEnumerable<T> records )
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

					if( record is IDynamicMetaObjectProvider dynamicObject )
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
						GetWriteRecordAction( record )( record );
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
				throw ex as CsvHelperException ?? new WriterException( context, "An unexpected error occurred.", ex );
			}
		}

		/// <summary>
		/// Adds the members from the mapping. This will recursively
		/// traverse the mapping tree and add all members for
		/// reference maps.
		/// </summary>
		/// <param name="members">The members to be added to.</param>
		/// <param name="mapping">The mapping where the members are added from.</param>
		protected virtual void AddMembers( MemberMapCollection members, ClassMap mapping )
		{
			members.AddRange( mapping.MemberMaps );
			foreach( var refMap in mapping.ReferenceMaps )
			{
				AddMembers( members, refMap.Data.Mapping );
			}
		}

		/// <summary>
		/// Creates a member expression for the given member on the record.
		/// This will recursively traverse the mapping to find the member
		/// and create a safe member accessor for each level as it goes.
		/// </summary>
		/// <param name="recordExpression">The current member expression.</param>
		/// <param name="mapping">The mapping to look for the member to map on.</param>
		/// <param name="memberMap">The member map to look for on the mapping.</param>
		/// <returns>An Expression to access the given member.</returns>
		protected virtual Expression CreateMemberExpression( Expression recordExpression, ClassMap mapping, MemberMap memberMap )
		{
			if( mapping.MemberMaps.Any( pm => pm == memberMap ) )
			{
				// The member is on this level.
				if( memberMap.Data.Member is PropertyInfo )
				{
					return Expression.Property( recordExpression, (PropertyInfo)memberMap.Data.Member );
				}

				if( memberMap.Data.Member is FieldInfo )
				{
					return Expression.Field( recordExpression, (FieldInfo)memberMap.Data.Member );
				}
			}

			// The member isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach( var refMap in mapping.ReferenceMaps )
			{
				var wrapped = refMap.Data.Member.GetMemberExpression( recordExpression );
				var memberExpression = CreateMemberExpression( wrapped, refMap.Data.Mapping, memberMap );
				if( memberExpression == null )
				{
					continue;
				}

				if( refMap.Data.Member.MemberType().GetTypeInfo().IsValueType )
				{
					return memberExpression;
				}

				var nullCheckExpression = Expression.Equal( wrapped, Expression.Constant( null ) );

				var isValueType = memberMap.Data.Member.MemberType().GetTypeInfo().IsValueType;
				var isGenericType = isValueType && memberMap.Data.Member.MemberType().GetTypeInfo().IsGenericType;
				Type memberType;
				if( isValueType && !isGenericType && !context.WriterConfiguration.UseNewObjectForNullReferenceMembers )
				{
					memberType = typeof( Nullable<> ).MakeGenericType( memberMap.Data.Member.MemberType() );
					memberExpression = Expression.Convert( memberExpression, memberType );
				}
				else
				{
					memberType = memberMap.Data.Member.MemberType();
				}

				var defaultValueExpression = isValueType && !isGenericType
					? (Expression)Expression.New( memberType )
					: Expression.Constant( null, memberType );
				var conditionExpression = Expression.Condition( nullCheckExpression, defaultValueExpression, memberExpression );
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
		protected virtual Action<T> GetWriteRecordAction<T>( T record )
		{
			var type = typeof( T );
			var typeKey = type.FullName;
			if( type == typeof( object ) )
			{
				type = record.GetType();
				typeKey += $"|{type.FullName}";
			}

			if( !context.TypeActions.TryGetValue( typeKey, out var action ) )
			{
				context.TypeActions[typeKey] = action = CreateWriteRecordAction( type, record );
			}

			return (Action<T>)action;
		}

		/// <summary>
		/// Creates the write record action for the given type if it
		/// doesn't already exist.
		/// </summary>
		/// <param name="type">The type of the custom class being written.</param>
		/// <param name="record">The record that will be written.</param>
		/// <typeparam name="T">The type of the record being written.</typeparam>
		protected virtual Action<T> CreateWriteRecordAction<T>( Type type, T record )
		{
			if( record is ExpandoObject expandoObject )
			{
				return CreateActionForExpandoObject<T>( expandoObject );
			}

			if( record is IDynamicMetaObjectProvider dynamicObject )
			{
				return CreateActionForDynamic<T>( dynamicObject );
			}

			if( context.WriterConfiguration.Maps[type] == null )
			{
				// We need to check again in case the header was not written.
				context.WriterConfiguration.Maps.Add( context.WriterConfiguration.AutoMap( type ) );
			}

			if( type.GetTypeInfo().IsPrimitive )
			{
				return CreateActionForPrimitive<T>( type );
			}

			return CreateActionForObject<T>( type );
		}

		/// <summary>
		/// Creates the action for an object.
		/// </summary>
		/// <param name="type">The type of object to create the action for.</param>
		protected virtual Action<T> CreateActionForObject<T>( Type type )
		{
			var recordParameter = Expression.Parameter( typeof( T ), "record" );
			var recordParameterConverted = Expression.Convert( recordParameter, type );

			// Get a list of all the members so they will
			// be sorted properly.
			var members = new MemberMapCollection();
			AddMembers( members, context.WriterConfiguration.Maps[type] );

			if( members.Count == 0 )
			{
				throw new WriterException( context, $"No properties are mapped for type '{type.FullName}'." );
			}

			var delegates = new List<Action<T>>();

			foreach( var memberMap in members )
			{
				if( memberMap.Data.WritingConvertExpression != null )
				{
					// The user is providing the expression to do the conversion.
					Expression exp = Expression.Invoke( memberMap.Data.WritingConvertExpression, recordParameterConverted );
					exp = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, exp );
					delegates.Add( Expression.Lambda<Action<T>>( exp, recordParameter ).Compile() );
					continue;
				}

				if( !CanWrite( memberMap ) )
				{
					continue;
				}

				Expression fieldExpression;

				if( memberMap.Data.IsConstantSet )
				{
					if( memberMap.Data.Constant == null )
					{
						fieldExpression = Expression.Constant( string.Empty );
					}
					else
					{
						fieldExpression = Expression.Constant( memberMap.Data.Constant );
						var typeConverterExpression = Expression.Constant( Configuration.TypeConverterFactory.GetConverter( memberMap.Data.Constant.GetType() ) );
						var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );
						fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
						fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( this ), Expression.Constant( memberMap.Data ) );
					}
				}
				else
				{
					if( memberMap.Data.TypeConverter == null )
					{
						// Skip if the type isn't convertible.
						continue;
					}

					fieldExpression = CreateMemberExpression( recordParameterConverted, context.WriterConfiguration.Maps[type], memberMap );

					var typeConverterExpression = Expression.Constant( memberMap.Data.TypeConverter );
					memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( memberMap.Data.Member.MemberType() ), memberMap.Data.TypeConverterOptions );
					memberMap.Data.TypeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;

					var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( this ), Expression.Constant( memberMap.Data ) );

					if( type.GetTypeInfo().IsClass )
					{
						var areEqualExpression = Expression.Equal( recordParameterConverted, Expression.Constant( null ) );
						fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );
					}
				}

				var writeFieldMethodCall = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, fieldExpression );

				delegates.Add( Expression.Lambda<Action<T>>( writeFieldMethodCall, recordParameter ).Compile() );
			}

			var action = CombineDelegates( delegates );

			return action;
		}

		/// <summary>
		/// Creates the action for a primitive.
		/// </summary>
		/// <param name="type">The type of primitive to create the action for.</param>
		protected virtual Action<T> CreateActionForPrimitive<T>( Type type )
		{
			var recordParameter = Expression.Parameter( typeof( T ), "record" );

			Expression fieldExpression = Expression.Convert( recordParameter, typeof( object ) );

			var typeConverter = Configuration.TypeConverterFactory.GetConverter( type );
			var typeConverterExpression = Expression.Constant( typeConverter );
			var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );

			var memberMapData = new MemberMapData( null )
			{
				Index = 0,
				TypeConverter = typeConverter,
				TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( type ) )
			};
			memberMapData.TypeConverterOptions.CultureInfo = context.WriterConfiguration.CultureInfo;

			fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( this ), Expression.Constant( memberMapData ) );
			fieldExpression = Expression.Call( Expression.Constant( this ), nameof( WriteConvertedField ), null, fieldExpression );

			var action = Expression.Lambda<Action<T>>( fieldExpression, recordParameter ).Compile();

			return action;
		}

		/// <summary>
		/// Creates an action for an ExpandoObject. This needs to be separate
		/// from other dynamic objects due to what seems to be an issue in ExpandoObject
		/// where expandos with the same members sometimes test as not equal.
		/// </summary>
		/// <param name="obj">The ExpandoObject.</param>
		/// <returns></returns>
		protected virtual Action<T> CreateActionForExpandoObject<T>( ExpandoObject obj )
		{
			Action<T> action = record =>
			{
				var dict = (IDictionary<string, object>)record;
				foreach( var val in dict.Values )
				{
					WriteField( val );
				}
			};

			return action;
		}

		/// <summary>
		/// Creates the action for a dynamic object.
		/// </summary>
		/// <param name="provider">The dynamic object.</param>
		protected virtual Action<T> CreateActionForDynamic<T>( IDynamicMetaObjectProvider provider )
		{
			// http://stackoverflow.com/a/14011692/68499

			var type = provider.GetType();
			var parameterExpression = Expression.Parameter( typeof( T ), "record" );

			var metaObject = provider.GetMetaObject( parameterExpression );
			var memberNames = metaObject.GetDynamicMemberNames();

			var delegates = new List<Action<T>>();
			foreach( var memberName in memberNames )
			{
				var getMemberBinder = (GetMemberBinder)Microsoft.CSharp.RuntimeBinder.Binder.GetMember( 0, memberName, type, new[] { CSharpArgumentInfo.Create( 0, null ) } );
				var getMemberMetaObject = metaObject.BindGetMember( getMemberBinder );
				var fieldExpression = getMemberMetaObject.Expression;
				fieldExpression = Expression.Call( Expression.Constant( this ), nameof( WriteField ), new[] { typeof( object ) }, fieldExpression );
				fieldExpression = Expression.Block( fieldExpression, Expression.Label( CallSiteBinder.UpdateLabel ) );
				var lambda = Expression.Lambda<Action<T>>( fieldExpression, parameterExpression );
				delegates.Add( lambda.Compile() );
			}

			var action = CombineDelegates( delegates );

			return action;
		}

		/// <summary>
		/// Combines the delegates into a single multicast delegate.
		/// This is needed because Silverlight doesn't have the
		/// Delegate.Combine( params Delegate[] ) overload.
		/// </summary>
		/// <param name="delegates">The delegates to combine.</param>
		/// <returns>A multicast delegate combined from the given delegates.</returns>
		protected virtual Action<T> CombineDelegates<T>( IEnumerable<Action<T>> delegates )
		{
			return (Action<T>)delegates.Aggregate<Delegate, Delegate>( null, Delegate.Combine );
		}

		/// <summary>
		/// Checks if the member can be written.
		/// </summary>
		/// <param name="memberMap">The member map that we are checking.</param>
		/// <returns>A value indicating if the member can be written.
		/// True if the member can be written, otherwise false.</returns>
		protected virtual bool CanWrite( MemberMap memberMap )
		{
			var cantWrite =
				// Ignored members.
				memberMap.Data.Ignore;

			if( memberMap.Data.Member is PropertyInfo property )
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
