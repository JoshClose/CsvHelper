#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections;
using System.Reflection;

namespace CsvHelper
{
	/// <summary>
	/// Used to compare properties by <see cref="CsvFieldAttribute" />.
	/// </summary>
	public class CsvPropertyInfoComparer : IComparer
	{
		private readonly bool useFieldName;

		/// <summary>
		/// Creates a new instance of CsvFieldAttributeComparer.
		/// </summary>
		/// <param name="useFieldName">True to compare by <see cref="CsvFieldAttribute.FieldName" />, otherwise compares by <see cref="CsvFieldAttribute.FieldIndex" />.</param>
		public CsvPropertyInfoComparer( bool useFieldName )
		{
			this.useFieldName = useFieldName;
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
		///                 </param><exception cref="T:System.ArgumentException">Neither <paramref name="x"/> nor <paramref name="y"/> implements the <see cref="T:System.IComparable"/> interface.
		///                     -or- 
		///                 <paramref name="x"/> and <paramref name="y"/> are of different types and neither one can handle comparisons with the other. 
		///                 </exception><filterpriority>2</filterpriority>
		public int Compare( object x, object y )
		{
			var xProperty = x as PropertyInfo;
			var yProperty = y as PropertyInfo;

			if( xProperty == null )
			{
				throw new ArgumentNullException( "x" );
			}
			if( yProperty == null )
			{
				throw new ArgumentNullException( "y" );
			}

			var xAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( xProperty, false );
			var yAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( yProperty, false );

			if( xAttribute == null && yAttribute == null )
			{
				return 0;
			}
			if( xAttribute == null )
			{
				return 1;
			}
			if( yAttribute == null )
			{
				return -1;
			}

			return useFieldName ? xAttribute.FieldName.CompareTo( yAttribute.FieldName ) : xAttribute.FieldIndex.CompareTo( yAttribute.FieldIndex );
		}
	}
}
