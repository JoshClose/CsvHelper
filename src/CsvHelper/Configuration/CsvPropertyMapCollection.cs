#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System.Collections;
using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// A collection that holds <see cref="CsvPropertyMap"/>'s.
	/// </summary>
	public class CsvPropertyMapCollection : IList<CsvPropertyMap>
	{
		private readonly List<CsvPropertyMap> list = new List<CsvPropertyMap>();

		public IEnumerator<CsvPropertyMap> GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Add( CsvPropertyMap item )
		{
			list.Add( item );

			// Always keep the list sorted.
			if( item.IndexValue > -1 )
			{
				list.Sort( new CsvPropertyMapComparer( false ) );
			}
		}

		public void Clear()
		{
			list.Clear();
		}

		public bool Contains( CsvPropertyMap item )
		{
			return list.Contains( item );
		}

		public void CopyTo( CsvPropertyMap[] array, int arrayIndex )
		{
			list.CopyTo( array, arrayIndex );
		}

		public bool Remove( CsvPropertyMap item )
		{
			return list.Remove( item );
		}

		public int Count
		{
			get { return list.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public int IndexOf( CsvPropertyMap item )
		{
			return list.IndexOf( item );
		}

		public void Insert( int index, CsvPropertyMap item )
		{
			list.Insert( index, item );
		}

		public void RemoveAt( int index )
		{
			list.RemoveAt( index );
		}

		public CsvPropertyMap this[ int index ]
		{
			get { return list[index]; }
			set { list[index] = value; }
		}
	}
}
