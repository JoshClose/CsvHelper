// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class properties to CSV fields.
	///</summary>
	public abstract class CsvClassMap
	{
		private readonly CsvPropertyMapCollection propertyMaps = new CsvPropertyMapCollection();
		private readonly List<CsvPropertyReferenceMap> referenceMaps = new List<CsvPropertyReferenceMap>();
		private int indexStart = -1;

		/// <summary>
		/// Where to start the index at when
		/// creating auto indexes for property maps.
		/// </summary>
		internal int IndexStart
		{
			get { return indexStart; }
			set { indexStart = value; }
		}

		/// <summary>
		/// Called to create the mappings.
		/// </summary>
		public abstract void CreateMap();

		/// <summary>
		/// Gets the constructor expression.
		/// </summary>
		public virtual NewExpression Constructor { get; protected set; } 

		/// <summary>
		/// The class property mappings.
		/// </summary>
		public virtual CsvPropertyMapCollection PropertyMaps
		{
			get { return propertyMaps; }
		}

		/// <summary>
		/// The class property reference mappings.
		/// </summary>
		public virtual List<CsvPropertyReferenceMap> ReferenceMaps
		{
			get { return referenceMaps; }
		}

		/// <summary>
		/// Allow only internal creation of CsvClassMap.
		/// </summary>
		internal CsvClassMap() {}

		/// <summary>
		/// Get the largest index for the
		/// properties and references.
		/// </summary>
		/// <returns>The max index.</returns>
		internal int GetMaxIndex()
		{
			if( PropertyMaps.Count == 0 && ReferenceMaps.Count == 0 )
			{
				return IndexStart;
			}

			var indexes = new List<int>();
			if( PropertyMaps.Count > 0 )
			{
				indexes.Add( PropertyMaps.Max( pm => pm.Data.Index ) );
			}
			indexes.AddRange( ReferenceMaps.Select( referenceMap => referenceMap.GetMaxIndex() ) );

			return indexes.Max();
		}
	}
}
