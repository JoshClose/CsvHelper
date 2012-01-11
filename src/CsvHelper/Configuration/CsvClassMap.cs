// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class properties to CSV fields.
	///</summary>
	public abstract class CsvClassMap
	{
		private readonly CsvPropertyMapCollection properties = new CsvPropertyMapCollection();

		/// <summary>
		/// The class property mappings.
		/// </summary>
		public CsvPropertyMapCollection Properties { get { return properties; } }
	}
}
