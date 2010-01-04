#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System.Collections.Generic;

namespace CsvHelper
{
	public class CsvFieldAttributeComparer : IComparer<CsvFieldAttribute>
	{
		private readonly bool useFieldName;

		/// <summary>
		/// Creates a new instance of CsvFieldAttributeComparer.
		/// </summary>
		/// <param name="useFieldName">True to compare by <see cref="CsvFieldAttribute.FieldName" />, otherwise compares by <see cref="CsvFieldAttribute.FieldIndex" />.</param>
		public CsvFieldAttributeComparer( bool useFieldName )
		{
			this.useFieldName = useFieldName;
		}

		public int Compare( CsvFieldAttribute x, CsvFieldAttribute y )
		{
			if( x == null && y == null )
			{
				return 0;
			}
			if( x == null )
			{
				return 1;
			}
			if( y == null )
			{
				return -1;
			}
			return useFieldName ? x.FieldName.CompareTo( y.FieldName ) : x.FieldIndex.CompareTo( y.FieldIndex );
		}
	}
}
