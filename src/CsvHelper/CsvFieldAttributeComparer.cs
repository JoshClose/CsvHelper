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
