// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Used to compare <see cref="CsvPropertyMap"/>s.
	/// The order is by field index ascending. Any
	/// fields that don't have an index are pushed
	/// to the bottom.
	/// </summary>
	public class CsvPropertyMapComparer : IComparer, IComparer<CsvPropertyMap>
	{
		/// <summary>
		/// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
		/// </summary>
		/// <returns>
		/// Value 
		///                     Condition 
		///                     Less than zero 
		///                 <paramref name="x"/> is less than <paramref name="y"/>. 
		///                     Zero 
		///                 <paramref name="x"/> equals <paramref name="y"/>. 
		///                     Greater than zero 
		///                 <paramref name="x"/> is greater than <paramref name="y"/>. 
		/// </returns>
		/// <param name="x">The first object to compare. 
		///                 </param><param name="y">The second object to compare. 
		///                 </param><exception cref="T:System.ArgumentException">Neither <paramref name="x"/> nor <paramref name="y"/> implements the <see cref="T:System.IComparable"/> interface.
		///                     -or- 
		///                 <paramref name="x"/> and <paramref name="y"/> are of different types and neither one can handle comparisons with the other. 
		///                 </exception><filterpriority>2</filterpriority>
		public virtual int Compare( object x, object y )
		{
			var xProperty = x as CsvPropertyMap;
			var yProperty = y as CsvPropertyMap;
			return Compare( xProperty, yProperty );
		}

		/// <summary>
		/// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
		/// </summary>
		/// <returns>
		/// Value 
		///                     Condition 
		///                     Less than zero
		///                 <paramref name="x"/> is less than <paramref name="y"/>.
		///                     Zero
		///                 <paramref name="x"/> equals <paramref name="y"/>.
		///                     Greater than zero
		///                 <paramref name="x"/> is greater than <paramref name="y"/>.
		/// </returns>
		/// <param name="x">The first object to compare.
		///                 </param><param name="y">The second object to compare.
		///                 </param>
		public virtual int Compare( CsvPropertyMap x, CsvPropertyMap y )
		{
			if( x == null )
			{
				throw new ArgumentNullException( "x" );
			}
			if( y == null )
			{
				throw new ArgumentNullException( "y" );
			}

			// Sorting order:
			// 1: Explicit indexing
			// 2: Names
			// 3: Neither

			if( x.Data.Index > -1 && y.Data.Index > -1 )
			{
				// Index compare
				return x.Data.Index.CompareTo( y.Data.Index );
			}

			if( x.Data.Names.Count > 0 && y.Data.Names.Count > 0 )
			{
				// Name compare
				var compareValue = CultureInfo.InvariantCulture.CompareInfo.Compare( x.Data.Names[0], y.Data.Names[0] );
				return compareValue == 0 ? x.Data.NameIndex.CompareTo( y.Data.NameIndex ) : compareValue;
			}

			if( x.Data.Index > 0 && y.Data.Names.Count > 0 )
			{
				// 1 < name
				return -1;
			}

			if( x.Data.Names.Count > 0 && y.Data.Index > 0 )
			{
				// name > 1
				return 1;
			}
			
			if( x.Data.Index == -1 && y.Data.Index == -1 )
			{
				// -1 == -1
				return 0;
			}

			if( x.Data.Index == -1 )
			{
				// -1 > 1/name
				return 1;
			}

			if( y.Data.Index == -1 )
			{
				// 1/name < -1
				return -1;
			}

			throw new CsvConfigurationException( "Property sorting fell through. This should never happen." );
		}
	}
}
