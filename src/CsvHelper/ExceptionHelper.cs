// Copyright 2009-2014 Josh Close and Contributors
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
	/// <summary>
	/// Common exception tasks.
	/// </summary>
	internal static class ExceptionHelper
	{
		/// <summary>
		/// Adds CsvHelper specific information to <see cref="Exception.Data"/>.
		/// </summary>
		/// <param name="exception">The exception to add the info to.</param>
		/// <param name="parser">The parser.</param>
		/// <param name="type">The type of object that was being created in the <see cref="CsvReader"/>.</param>
		/// <param name="namedIndexes">The named indexes in the <see cref="CsvReader"/>.</param>
		/// <param name="currentIndex">The current index of the <see cref="CsvReader"/>.</param>
		/// <param name="currentRecord">The current record of the <see cref="CsvReader"/>.</param>
		public static void AddExceptionDataMessage( Exception exception, ICsvParser parser, Type type, Dictionary<string, List<int>> namedIndexes, int? currentIndex, string[] currentRecord )
		{
			// An error could occur in the parser and get this message set on it, then occur in the
			// reader and have it set again. This is ok because when the reader calls this method,
			// it will have extra info to be added.

			try
			{
				exception.Data["CsvHelper"] = GetErrorMessage( parser, type, namedIndexes, currentIndex, currentRecord );
			}
			catch( Exception ex )
			{
				var exString = new StringBuilder();
				exString.AppendLine( "An error occurred while creating exception details." );
				exString.AppendLine();
				exString.AppendLine( ex.ToString() );
				exception.Data["CsvHelper"] = exString.ToString();
			}
		}

		/// <summary>
		/// Gets CsvHelper information to be added to an exception.
		/// </summary>
		/// <param name="parser">The parser.</param>
		/// <param name="type">The type of object that was being created in the <see cref="CsvReader"/>.</param>
		/// <param name="namedIndexes">The named indexes in the <see cref="CsvReader"/>.</param>
		/// <param name="currentIndex">The current index of the <see cref="CsvReader"/>.</param>
		/// <param name="currentRecord">The current record of the <see cref="CsvReader"/>.</param>
		/// <returns>The CsvHelper information.</returns>
		public static string GetErrorMessage( ICsvParser parser, Type type, Dictionary<string, List<int>> namedIndexes, int? currentIndex, string[] currentRecord )
		{
			var messageInfo = new StringBuilder();

			if( parser != null )
			{
				messageInfo.AppendFormat( "Row: '{0}' (1 based)", parser.Row ).AppendLine();
			}

			if( type != null )
			{
				messageInfo.AppendFormat( "Type: '{0}'", type.FullName ).AppendLine();
			}

			if( currentIndex.HasValue )
			{
				messageInfo.AppendFormat( "Field Index: '{0}' (0 based)", currentIndex ).AppendLine();
			}

#if !NET_2_0
			if( namedIndexes != null )
			{
				var fieldName = ( from pair in namedIndexes
				                  from index in pair.Value
				                  where index == currentIndex
				                  select pair.Key ).SingleOrDefault();
				if( fieldName != null )
				{
					messageInfo.AppendFormat( "Field Name: '{0}'", fieldName ).AppendLine();
				}
			}
#endif

			if( currentRecord != null && currentIndex > -1 && currentIndex < currentRecord.Length )
			{
				if( currentIndex.Value < currentRecord.Length )
				{
					var fieldValue = currentRecord[currentIndex.Value];
					messageInfo.AppendFormat( "Field Value: '{0}'", fieldValue ).AppendLine();
				}
			}

			return messageInfo.ToString();
		}
	}
}
