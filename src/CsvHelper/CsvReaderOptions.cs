#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Reflection;

namespace CsvHelper
{
	/// <summary>
	/// Options for the <see cref="CsvReader" />.
	/// </summary>
	public class CsvReaderOptions
	{
		private bool hasHeaderRecord = true;
		private BindingFlags propertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;

		/// <summary>
		/// Gets or sets the strict reading flag.
		/// True to enable strict reading, otherwise false.
		/// Strict reading will cause a <see cref="MissingFieldException" />
		/// to be thrown if a named index is not found.
		/// </summary>
		public bool Strict { get; set; }

		/// <summary>
		/// Gets or sets the property binding flags.
		/// This determines what properties on the custom
		/// class are used when reading records. Default
		/// is Public | Instance.
		/// </summary>
		public BindingFlags PropertyBindingFlags
		{
			get { return propertyBindingFlags; }
			set { propertyBindingFlags = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file being read has a header record.
		/// Default is true.
		/// </summary>
		public bool HasHeaderRecord
		{
			get { return hasHeaderRecord; }
			set { hasHeaderRecord = value; }
		}
	}
}
