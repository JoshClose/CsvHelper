using System;
using System.Collections;
using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Used to compare properties.
	/// </summary>
	internal class CsvPropertyMapComparer : IComparer, IComparer<CsvPropertyMap>
	{
		private readonly bool useFieldName;

		/// <summary>
		/// Creates a new instance of CsvPropertyMapComparer.
		/// </summary>
		/// <param name="useFieldName">True to compare by Name, otherwise compares by Index.</param>
		public CsvPropertyMapComparer( bool useFieldName )
		{
			this.useFieldName = useFieldName;
		}

		public int Compare( object x, object y )
		{
			var xProperty = x as CsvPropertyMap;
			var yProperty = y as CsvPropertyMap;
			return Compare( xProperty, yProperty );
		}

		public int Compare( CsvPropertyMap x, CsvPropertyMap y )
		{
			if( x == null )
			{
				throw new ArgumentNullException( "x" );
			}
			if( y == null )
			{
				throw new ArgumentNullException( "y" );
			}

			if( !useFieldName )
			{
				if( x.IndexValue == -1 && y.IndexValue == -1 )
				{
					return 0;
				}
				if( x.IndexValue == -1 )
				{
					return 1;
				}
				if( y.IndexValue == -1 )
				{
					return -1;
				}
			}

			return useFieldName ? x.NameValue.CompareTo( y.NameValue ) : x.IndexValue.CompareTo( y.IndexValue );
		}
	}
}
