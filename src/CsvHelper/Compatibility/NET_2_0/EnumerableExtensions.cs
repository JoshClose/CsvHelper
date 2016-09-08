// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if NET_2_0
using System;
using System.Collections;
using System.Collections.Generic;

namespace System.Linq
{
	internal static class EnumerableExtensions
	{
		public static bool Contains<T>( this IEnumerable<T> enumerable, T value )
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

		public static bool Any<TSource>( this IEnumerable<TSource> enumerable, Func<TSource, bool> predicate )
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

		public static bool All<TSource>( this IEnumerable<TSource> enumerable, Func<TSource, bool> predicate )
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

		public static bool SequenceEqual( this IEnumerable x, IEnumerable y )
		{
			if( x == null )
			{
				throw new ArgumentNullException( "x" );
			}

			if( y == null )
			{
				throw new ArgumentNullException( "y" );
			}

			var xEnumerator = x.GetEnumerator();
			var yEnumerator = y.GetEnumerator();

			while( true )
			{
				var xMoved = xEnumerator.MoveNext();
				var yMoved = yEnumerator.MoveNext();

				if( xMoved != yMoved )
				{
					return false;
				}

				if( xMoved == false )
				{
					break;
				}

				if( xEnumerator.Current == null && yEnumerator.Current != null || 
					xEnumerator.Current != null && yEnumerator.Current == null )
				{
					return false;
				}

				if( xEnumerator.Current == null && yEnumerator.Current == null )
				{
					return true;
				}

				if( !xEnumerator.Current.Equals( yEnumerator.Current ) )
				{
					return false;
				}
			}

			return true;
		}
	}
}
#endif
