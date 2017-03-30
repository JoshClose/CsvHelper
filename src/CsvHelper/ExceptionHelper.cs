// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CsvHelper
{
	/// <summary>
	/// Common exception tasks.
	/// </summary>
	internal static class ExceptionHelper
	{
		public static void AddExceptionData( CsvHelperException exception, int row, Type type, int? currentIndex, Dictionary<string, List<int>> namedIndexes, string[] currentRecord )
		{
			exception.Row = row;
			exception.Type = type;
			exception.FieldIndex = currentIndex ?? -1;

			if( namedIndexes != null )
			{
				var fieldName = ( from pair in namedIndexes
								  from index in pair.Value
								  where index == currentIndex
								  select pair.Key ).SingleOrDefault();
				if( fieldName != null )
				{
					exception.FieldName = fieldName;
				}
			}

			if( currentRecord != null && currentIndex > -1 && currentIndex < currentRecord.Length )
			{
				if( currentIndex.Value < currentRecord.Length )
				{
					var fieldValue = currentRecord[currentIndex.Value];
					exception.FieldValue = fieldValue;
				}
			}
		}
	}
}
