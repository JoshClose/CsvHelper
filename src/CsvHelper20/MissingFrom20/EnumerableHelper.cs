// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;

namespace CsvHelper.MissingFrom20
{
	internal static class EnumerableHelper
	{
		public static bool Contains<T>( IEnumerable<T> enumerable, T value )
		{
			foreach( var item in enumerable )
			{
				if( value.Equals( item ) )
				{
					return true;
				}
			}
			return false;
		}

		public static bool Any<TSource>( IEnumerable<TSource> enumerable, Func<TSource, bool> predicate )
		{
			foreach( var item in enumerable )
			{
				if( predicate( item ) )
				{
					return true;
				}
			}

			return false;
		}

		public static bool All<TSource>( IEnumerable<TSource> enumerable, Func<TSource, bool> predicate )
		{
			foreach( var item in enumerable )
			{
				if( !predicate( item ) )
				{
					return false;
				}
			}

			return true;
		}
	}
}
