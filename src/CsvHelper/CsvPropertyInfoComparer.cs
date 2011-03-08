#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;

namespace CsvHelper
{
	/// <summary>
	/// Used to compare properties by <see cref="CsvFieldAttribute" />.
	/// </summary>
    public class CsvPropertyInfoComparer : IComparer<CsvPropertyInfo>
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
		
	    public int Compare(CsvPropertyInfo x, CsvPropertyInfo y)
	    {            
            if (x == null)
            {
                throw new ArgumentNullException("x");
            }
            if (y == null)
            {
                throw new ArgumentNullException("y");
            }
            
            // Push properties without the attribute to the bottom.
            if (!x.HasAttribute && !y.HasAttribute)
            {
                return 0;
            }
            if (!x.HasAttribute)
            {
                return 1;
            }
            if (!y.HasAttribute)
            {
                return -1;
            }

            if (!useFieldName)
            {
                // Treat non-set field indexes like nulls.
                if (x.FieldIndex == -1 && y.FieldIndex == -1)
                {
                    return 0;
                }
                if (x.FieldIndex == -1)
                {
                    return 1;
                }
                if (y.FieldIndex == -1)
                {
                    return -1;
                }
            }

            return useFieldName ? x.Name.CompareTo(y.Name) : x.FieldIndex.CompareTo(y.FieldIndex);
	    }
	}
}
