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
using CsvHelper.Expressions;

#pragma warning disable 649
#pragma warning disable 169

namespace CsvHelper
{
	/// <summary>
	/// Used to write CSV files.
	/// </summary>
	public class CsvWriter : IWriter
	{
		private readonly RecordManager recordManager;
		private IWritingContext context;
		private bool disposed;
		private ISerializer serializer;

		/// <summary>
		/// Gets the writing context.
		/// </summary>
		public virtual IWritingContext Context => context;

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
			context = serializer.Context as IWritingContext ?? throw new InvalidOperationException( $"For {nameof( ISerializer )} to be used in {nameof( CsvWriter )}, {nameof( ISerializer.Context )} must also implement {nameof( IWritingContext )}." );
			recordManager = ObjectResolver.Current.Resolve<RecordManager>( this );
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
			var converter = Configuration.TypeConverterCache.GetConverter( type );
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
				typeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions { CultureInfo = context.WriterConfiguration.CultureInfo }, context.WriterConfiguration.TypeConverterOptionsCache.GetOptions( type ) );
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
			var converter = Configuration.TypeConverterCache.GetConverter<TConverter>();
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
			members.AddMembers( context.WriterConfiguration.Maps[type] );

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
				recordManager.Write( record );
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
		public virtual void WriteRecords( IEnumerable records )
		{
			// Changes in this method require changes in method WriteRecords<T>( IEnumerable<T> records ) also.

			try
			{
				foreach( var record in records )
				{
					var recordType = record.GetType();

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
						recordManager.Write( record );
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
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">Record type.</typeparam>
		/// <param name="records">The list of records to write.</param>
		public virtual void WriteRecords<T>( IEnumerable<T> records )
		{
			// Changes in this method require changes in method WriteRecords( IEnumerable records ) also.

			try
			{
				// Write the header. If records is a List<dynamic>, the header won't be written.
				// This is because typeof( T ) = Object.
				var recordType = typeof( T );
				var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
				if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten && !isPrimitive && recordType != typeof( object ) )
				{
					WriteHeader( recordType );
					if( context.HasHeaderBeenWritten )
					{
						NextRecord();
					}
				}

				var getRecordType = recordType == typeof( object );
				foreach( var record in records )
				{
					if( getRecordType )
					{
						recordType = record.GetType();
					}

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
						isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if( context.WriterConfiguration.HasHeaderRecord && !context.HasHeaderBeenWritten && !isPrimitive )
						{
							WriteHeader( recordType );
							NextRecord();
						}
					}

					try
					{
						recordManager.Write( record );
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
		/// Checks if the member can be written.
		/// </summary>
		/// <param name="memberMap">The member map that we are checking.</param>
		/// <returns>A value indicating if the member can be written.
		/// True if the member can be written, otherwise false.</returns>
		public virtual bool CanWrite( MemberMap memberMap )
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

		/// <summary>
		/// Gets the type for the record. If the generic type
		/// is an object due to boxing, it will call GetType()
		/// on the record itself.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		public virtual Type GetTypeForRecord<T>( T record )
		{
			var type = typeof( T );
			if( type == typeof( object ) )
			{
				type = record.GetType();
			}

			return type;
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

			Flush();

			if( disposing )
			{
				serializer?.Dispose();
			}

			serializer = null;
			context = null;
			disposed = true;
		}
	}
}
