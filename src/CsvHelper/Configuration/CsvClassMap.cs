// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class properties to CSV fields.
	///</summary>
	public abstract class CsvClassMap
	{
		private readonly CsvPropertyMapCollection propertyMaps = new CsvPropertyMapCollection();
		private readonly List<CsvPropertyReferenceMap> referenceMaps = new List<CsvPropertyReferenceMap>();

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
	}
}
