// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

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
	}
}
