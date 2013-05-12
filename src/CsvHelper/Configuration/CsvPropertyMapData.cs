// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// The configured data for the property map.
	/// </summary>
	public class CsvPropertyMapData
	{
		private readonly List<string> names = new List<string>();
		private int index = -1;
		private object defaultValue;

		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> that the data
		/// is associated with.
		/// </summary>
		public virtual PropertyInfo Property
		{
			get; private set;
		}

		/// <summary>
		/// Gets the list of column names.
		/// </summary>
		public virtual List<string> Names
		{
			get { return names; }
		}

		/// <summary>
		/// Gets or sets the column index.
		/// </summary>
		public virtual int Index
		{
			get { return index; }
			set { index = value; }
		}

		/// <summary>
		/// Gets or sets the type converter.
		/// </summary>
		public virtual ITypeConverter TypeConverter { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the field should be ignored.
		/// </summary>
		public virtual bool Ignore { get; set; }

		/// <summary>
		/// Gets or sets the default value used when a CSV field is empty.
		/// </summary>
		public virtual object Default
		{
			get { return defaultValue; }
			set
			{
				defaultValue = value;
				IsDefaultSet = true;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this instance is default value set.
		/// </summary>
		public virtual bool IsDefaultSet { get; set; }

		/// <summary>
		/// Gets or sets the format used when converting the value to string.
		/// </summary>
		public virtual string Format { get; set; }

		/// <summary>
		/// Gets or sets the expression used to convert data in the
		/// row to the property.
		/// </summary>
		public virtual Expression ConvertExpression { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyMapData"/> class.
		/// </summary>
		/// <param name="property">The property.</param>
		public CsvPropertyMapData( PropertyInfo property )
		{
			Property = property;
		}
	}
}
