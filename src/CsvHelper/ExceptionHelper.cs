// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Text;
#if !NET_2_0
using System.Linq;
#endif

namespace CsvHelper
{
	internal static class ExceptionHelper
	{
		/// <summary>
		/// Gets a new exception with more detailed information.
		/// </summary>
		/// <typeparam name="TException">The type of the exception to create.</typeparam>
		/// <param name="message">The exception message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <param name="parser">The <see cref="ICsvParser" />.</param>
		/// <param name="type">The type the <see cref="ICsvReader" /> was trying to create.</param>
		/// <param name="namedIndexes">The readers named indexes.</param>
		/// <param name="currentIndex">The current reader index.</param>
		/// <param name="currentRecord">The current reader record.</param>
		/// <returns></returns>
		public static TException GetReaderException<TException>( string message, Exception innerException, ICsvParser parser, Type type, Dictionary<string, List<int>> namedIndexes, int? currentIndex, string[] currentRecord )
			where TException : CsvHelperException
		{
			if( string.IsNullOrEmpty( message ) )
			{
				throw new ArgumentNullException( "message" );
			}

			var messageInfo = new StringBuilder();

			messageInfo.AppendLine( message );
			messageInfo.AppendLine();

			messageInfo.AppendFormat( "Row: '{0}' (1 based)", parser.Row ).AppendLine();

			if( type != null )
			{
				messageInfo.AppendFormat( "Type: '{0}'", type.FullName ).AppendLine();
			}

			if( currentIndex.HasValue )
			{
				messageInfo.AppendFormat( "Field Index: '{0}' (0 based)", currentIndex ).AppendLine();
			}

			string fieldName = null;
#if !NET_2_0
			if( parser.Configuration.HasHeaderRecord && namedIndexes != null )
			{
				fieldName = ( from pair in namedIndexes
							  from index in pair.Value
							  where index == currentIndex
							  select pair.Key ).SingleOrDefault();
				if( fieldName != null )
				{
					messageInfo.AppendFormat( "Field Name: '{0}'", fieldName ).AppendLine();
				}
			}
#endif

			string fieldValue = null;
			if( currentIndex.HasValue && currentRecord != null && currentIndex < currentRecord.Length )
			{
				fieldValue = currentRecord[currentIndex.Value];
				messageInfo.AppendFormat( "Field Value: '{0}'", currentRecord[currentIndex.Value] ).AppendLine();
			}

			var exception = (TException)Activator.CreateInstance( typeof( TException ), messageInfo.ToString(), innerException );

			var parserInfo = exception as ICsvParserExceptionInfo;
			if( parserInfo != null )
			{
				parserInfo.BytePosition = parser.BytePosition;
				parserInfo.CharPosition = parser.CharPosition;
				parserInfo.Row = parser.Row;
			}

			var readerInfo = exception as ICsvReaderExceptionInfo;
			if( readerInfo != null )
			{
				readerInfo.FieldIndex = currentIndex.GetValueOrDefault();
				readerInfo.FieldName = fieldName;
				readerInfo.FieldValue = fieldValue;
			}

			return exception;
		}
	}
}
