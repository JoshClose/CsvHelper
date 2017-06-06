// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Reads fields from a <see cref="TextReader"/>.
	/// </summary>
	public class FieldReader : IDisposable
	{
		private char[] buffer;
		private StringBuilder rawRecord = new StringBuilder();
		private StringBuilder field = new StringBuilder();
		private int bufferPosition;
		private int fieldStartPosition;
		private int fieldEndPosition;
		private int rawRecordStartPosition;
		private int rawRecordEndPosition;
		private int charsRead;
		private bool disposed;
		private readonly ICsvParserConfiguration configuration;
		private long charPosition;
		private long bytePosition;
		private TextReader reader;
		private bool isFieldBad;

		/// <summary>
		/// Gets the character position.
		/// </summary>
		public long CharPosition => charPosition;

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		public long BytePosition => bytePosition;

		/// <summary>
		/// Gets all the characters of the record including
		/// quotes, delimeters, and line endings.
		/// </summary>
		public string RawRecord => rawRecord.ToString();

		/// <summary>
		/// Gets the <see cref="TextReader"/> that is read from.
		/// </summary>
		public TextReader Reader => reader;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public ICsvParserConfiguration Configuration => configuration;

		/// <summary>
		/// Gets or sets a value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// </summary>
		public bool IsFieldBad
		{
			get { return isFieldBad; }
			set { isFieldBad = value; }
		}

		/// <summary>
		/// Creates a new <see cref="FieldReader"/> using the given
		/// <see cref="TextReader"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="configuration"></param>
		public FieldReader( TextReader reader, ICsvParserConfiguration configuration )
		{
			this.reader = reader;
			buffer = new char[0];
			this.configuration = configuration;
		}

		/// <summary>
		/// Gets the next char as an <see cref="int"/>.
		/// </summary>
		/// <returns></returns>
		public virtual int GetChar()
		{
			if( bufferPosition >= charsRead )
			{
				if( buffer.Length == 0 )
				{
					buffer = new char[configuration.BufferSize];
				}

				if( charsRead > 0 )
				{
					// Create a new buffer with extra room for what is left from
					// the old buffer. Copy the remaining contents onto the new buffer.
					var bufferLeft = charsRead - rawRecordStartPosition;
					var bufferUsed = charsRead - bufferLeft;
					var tempBuffer = new char[bufferLeft + configuration.BufferSize];
					Array.Copy( buffer, rawRecordStartPosition, tempBuffer, 0, bufferLeft );
					buffer = tempBuffer;

					bufferPosition = bufferPosition - bufferUsed;
					fieldStartPosition = fieldStartPosition - bufferUsed;
					fieldEndPosition = Math.Max( fieldEndPosition - bufferUsed, 0 );
					rawRecordStartPosition = rawRecordStartPosition - bufferUsed;
					rawRecordEndPosition = rawRecordEndPosition - bufferUsed;
				}

				charsRead = Reader.Read( buffer, bufferPosition, configuration.BufferSize );
				if( charsRead == 0 )
				{
					// End of file.
					return -1;
				}

				// Add the char count from the previous buffer that was copied onto this one.
				charsRead += bufferPosition;
			}

			var c = buffer[bufferPosition];
			bufferPosition++;
			rawRecordEndPosition = bufferPosition;

			charPosition++;

			return c;
		}

		/// <summary>
		/// Gets the field. This will append any reading progress.
		/// </summary>
		/// <returns>The current field.</returns>
		public virtual string GetField()
		{
			AppendField();

			if( isFieldBad && configuration.ThrowOnBadData )
			{
				throw new CsvBadDataException( $"Field: '{field}'" );
			}

			if( isFieldBad )
			{
				configuration.BadDataCallback?.Invoke( field.ToString() );
			}

			isFieldBad = false;

			var result = field.ToString();
			field.Clear();

			return result;
		}

		/// <summary>
		/// Appends the current reading progress.
		/// </summary>
		public virtual void AppendField()
		{
			if( configuration.CountBytes )
			{
				bytePosition += configuration.Encoding.GetByteCount( buffer, rawRecordStartPosition, bufferPosition - rawRecordStartPosition );
			}

			rawRecord.Append( new string( buffer, rawRecordStartPosition, rawRecordEndPosition - rawRecordStartPosition ) );
			rawRecordStartPosition = rawRecordEndPosition;

			var length = fieldEndPosition - fieldStartPosition;
			field.Append( new string( buffer, fieldStartPosition, length ) );
			fieldStartPosition = bufferPosition;
			fieldEndPosition = 0;
		}

		/// <summary>
		/// Sets the start of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		public virtual void SetFieldStart( int offset = 0 )
		{
			var position = bufferPosition + offset;
			if( position >= 0 )
			{
				fieldStartPosition = position;
			}
		}

		/// <summary>
		/// Sets the end of the field to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the field start.
		/// The offset should be less than 1.</param>
		public virtual void SetFieldEnd( int offset = 0 )
		{
			var position = bufferPosition + offset;
			if( position >= 0 )
			{
				fieldEndPosition = position;
			}
		}

		/// <summary>
		/// Sets the raw record end to the current buffer position.
		/// </summary>
		/// <param name="offset">An offset for the raw record end.
		/// The offset should be less than 1.</param>
		public virtual void SetRawRecordEnd( int offset )
		{
			var position = bufferPosition + offset;
			if( position >= 0 )
			{
				rawRecordEndPosition = position;
			}
		}

		/// <summary>
		/// Clears the raw record.
		/// </summary>
		public virtual void ClearRawRecord()
		{
			rawRecord.Clear();
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
			if( disposed )
			{
				return;
			}

			if( disposing )
			{
				Reader?.Dispose();
			}

			disposed = true;
			reader = null;
		}
	}
}
