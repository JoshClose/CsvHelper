// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public class CsvParser : IParser
	{
		private ReadingContext context;
		private IFieldReader fieldReader;
		private bool disposed;

		/// <summary>
		/// Gets the reading context.
		/// </summary>
		public virtual IParserContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ICsvParserConfiguration Configuration => context.ParserConfiguration;

		/// <summary>
		/// Gets the <see cref="FieldReader"/>.
		/// </summary>
		public virtual IFieldReader FieldReader => fieldReader;

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		public CsvParser( TextReader reader ) : this( new CsvFieldReader( reader, new CsvConfiguration(), false ) ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser( TextReader reader, bool leaveOpen ) : this( new CsvFieldReader( reader, new CsvConfiguration(), false ) ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser( TextReader reader, CsvConfiguration configuration ) : this( new CsvFieldReader( reader, configuration, false ) ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser( TextReader reader, CsvConfiguration configuration, bool leaveOpen ) : this( new CsvFieldReader( reader, configuration, leaveOpen ) ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="FieldReader"/>.
		/// </summary>
		/// <param name="fieldReader">The field reader.</param>
		public CsvParser( IFieldReader fieldReader )
		{
			this.fieldReader = fieldReader ?? throw new ArgumentNullException( nameof( fieldReader ) );
			context = fieldReader.Context as ReadingContext ?? throw new InvalidOperationException( "For FieldReader to be used in CsvParser, FieldReader.Context must also implement IParserContext." );
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		public virtual string[] Read()
		{
			try
			{
				context.ClearCache( Caches.RawRecord );

				var row = ReadLine();

				return row;
			}
			catch( Exception ex )
			{
				throw ex as CsvHelperException ?? new CsvParserException( context, "An unexpected error occurred.", ex );
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
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
				fieldReader?.Dispose();
			}

			fieldReader = null;
			context = null;
			disposed = true;
		}

		/// <summary>
		/// Reads a line of the CSV file.
		/// </summary>
		/// <returns>The CSV line.</returns>
		protected virtual string[] ReadLine()
		{
			context.RecordBuilder.Clear();
			context.Row++;
			context.RawRow++;

			while( true )
			{
				context.C = fieldReader.GetChar();

				if( context.C == -1 )
				{
					// We have reached the end of the file.
					if( context.RecordBuilder.Length > 0 )
					{
						// There was no line break at the end of the file.
						// We need to return the last record first.
						context.RecordBuilder.Add( fieldReader.GetField() );
						return context.RecordBuilder.ToArray();
					}

					return null;
				}

				if( context.RecordBuilder.Length == 0 && ( ( context.C == context.ParserConfiguration.Comment && context.ParserConfiguration.AllowComments ) || context.C == '\r' || context.C == '\n' ) )
				{
					ReadBlankLine();
					if( !context.ParserConfiguration.IgnoreBlankLines )
					{
						break;
					}

					continue;
				}

				if( context.C == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes )
				{
					if( ReadQuotedField() )
					{
						break;
					}
				}
				else
				{
					if( ReadField() )
					{
						break;
					}
				}
			}

			return context.RecordBuilder.ToArray();
		}

		/// <summary>
		/// Reads a blank line. This accounts for empty lines
		/// and commented out lines.
		/// </summary>
		protected virtual void ReadBlankLine()
		{
			if( context.ParserConfiguration.IgnoreBlankLines )
			{
				context.Row++;
			}

			while( true )
			{
				if( context.C == '\r' || context.C == '\n' )
				{
					ReadLineEnding();
					fieldReader.SetFieldStart();
					return;
				}

				if( context.C == -1 )
				{
					return;
				}

				// If the buffer runs, it appends the current data to the field.
				// We don't want to capture any data on a blank line, so we
				// need to set the field start every char.
				fieldReader.SetFieldStart();
				context.C = fieldReader.GetChar();
			}
		}

		/// <summary>
		/// Reads until a delimiter or line ending is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadField()
		{
			if( context.C != context.ParserConfiguration.Delimiter[0] && context.C != '\r' && context.C != '\n' )
			{
				context.C = fieldReader.GetChar();
			}

			while( true )
			{
				if( context.C == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes )
				{
					context.IsFieldBad = true;
				}

				if( context.C == context.ParserConfiguration.Delimiter[0] )
				{
					fieldReader.SetFieldEnd( -1 );

					// End of field.
					if( ReadDelimiter() )
					{
						// Set the end of the field to the char before the delimiter.
						context.RecordBuilder.Add( fieldReader.GetField() );
						return false;
					}
				}
				else if( context.C == '\r' || context.C == '\n' )
				{
					// End of line.
					fieldReader.SetFieldEnd( -1 );
					var offset = ReadLineEnding();
					fieldReader.SetRawRecordEnd( offset );
					context.RecordBuilder.Add( fieldReader.GetField() );

					fieldReader.SetFieldStart( offset );

					return true;
				}
				else if( context.C == -1 )
				{
					// End of file.
					fieldReader.SetFieldEnd();
					context.RecordBuilder.Add( fieldReader.GetField() );
					return true;
				}

				context.C = fieldReader.GetChar();
			}
		}

		/// <summary>
		/// Reads until the field is not quoted and a delimeter is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadQuotedField()
		{
			var inQuotes = true;
			// Set the start of the field to after the quote.
			fieldReader.SetFieldStart();

			while( true )
			{
				// 1,"2" ,3

				var cPrev = context.C;
				context.C = fieldReader.GetChar();
				if( context.C == context.ParserConfiguration.Quote )
				{
					inQuotes = !inQuotes;

					if( !inQuotes )
					{
						// Add an offset for the quote.
						fieldReader.SetFieldEnd( -1 );
						fieldReader.AppendField();
						fieldReader.SetFieldStart();
					}

					continue;
				}

				if( inQuotes )
				{
					if( context.C == '\r' || ( context.C == '\n' && cPrev != '\r' ) )
					{
						// Inside a quote \r\n is just another character to absorb.
						context.RawRow++;
					}

					if( context.C == -1 )
					{
						fieldReader.SetFieldEnd();
						context.RecordBuilder.Add( fieldReader.GetField() );
						return true;
					}
				}

				if( !inQuotes )
				{
					if( context.C == context.ParserConfiguration.Delimiter[0] )
					{
						fieldReader.SetFieldEnd( -1 );

						if( ReadDelimiter() )
						{
							// Add an extra offset because of the end quote.
							context.RecordBuilder.Add( fieldReader.GetField() );
							return false;
						}
					}
					else if( context.C == '\r' || context.C == '\n' )
					{
						fieldReader.SetFieldEnd( -1 );
						var offset = ReadLineEnding();
						fieldReader.SetRawRecordEnd( offset );
						context.RecordBuilder.Add( fieldReader.GetField() );
						fieldReader.SetFieldStart( offset );
						return true;
					}
					else if( cPrev == context.ParserConfiguration.Quote )
					{
						// We're out of quotes. Read the reset of
						// the field like a normal field.
						return ReadField();
					}
				}
			}
		}

		/// <summary>
		/// Reads until the delimeter is done.
		/// </summary>
		/// <returns>True if a delimiter was read. False if the sequence of
		/// chars ended up not being the delimiter.</returns>
		protected virtual bool ReadDelimiter()
		{
			if( context.C != context.ParserConfiguration.Delimiter[0] )
			{
				throw new InvalidOperationException( "Tried reading a delimiter when the first delimiter char didn't match the current char." );
			}

			if( context.ParserConfiguration.Delimiter.Length == 1 )
			{
				return true;
			}

			for( var i = 1; i < context.ParserConfiguration.Delimiter.Length; i++ )
			{
				context.C = fieldReader.GetChar();
				if( context.C != context.ParserConfiguration.Delimiter[i] )
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Reads until the line ending is done.
		/// </summary>
		/// <returns>True if more chars were read, otherwise false.</returns>
		protected virtual int ReadLineEnding()
		{
			var fieldStartOffset = 0;
			if( context.C == '\r' )
			{
				context.C = fieldReader.GetChar();
				if( context.C != '\n' && context.C != -1 )
				{
					// The start needs to be moved back.
					fieldStartOffset--;
				}
			}

			return fieldStartOffset;
		}
	}
}