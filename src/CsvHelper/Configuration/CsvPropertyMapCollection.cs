using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
