// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Used to compare <see cref="MemberMap"/>s.
	/// The order is by field index ascending. Any
	/// fields that don't have an index are pushed
	/// to the bottom.
	/// </summary>
	internal class MemberMapComparer : IComparer<MemberMap>
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
			var xMember = x as MemberMap;
			var yMember = y as MemberMap;
			return Compare( xMember, yMember );
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
		public virtual int Compare( MemberMap x, MemberMap y )
		{
			if( x == null )
			{
				throw new ArgumentNullException( nameof( x ) );
			}
			if( y == null )
			{
				throw new ArgumentNullException( nameof( y ) );
			}

			return x.Data.Index.CompareTo( y.Data.Index );
		}
	}
}
