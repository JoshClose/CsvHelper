// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0
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
		private readonly CsvPropertyNameCollection names = new CsvPropertyNameCollection();
		private int index = -1;
		private object defaultValue;
		private readonly TypeConverterOptions typeConverterOptions = new TypeConverterOptions();

		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> that the data
		/// is associated with.
		/// </summary>
		public virtual PropertyInfo Property { get; private set; }

		/// <summary>
		/// Gets the list of column names.
		/// </summary>
		public virtual CsvPropertyNameCollection Names
		{
			get { return names; }
		}

		/// <summary>
		/// Gets or sets the index of the name.
		/// This is used if there are multiple
		/// columns with the same names.
		/// </summary>
		public virtual int NameIndex { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if the name was
		/// explicitly set. True if it was explicity set,
		/// otherwise false.
		/// </summary>
		public virtual bool IsNameSet { get; set; }

		/// <summary>
		/// Gets or sets the column index.
		/// </summary>
		public virtual int Index
		{
			get { return index; }
			set { index = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if the index was
		/// explicitly set. True if it was explicitly set,
		/// otherwise false.
		/// </summary>
		public virtual bool IsIndexSet { get; set; }

		/// <summary>
		/// Gets or sets the type converter.
		/// </summary>
		public virtual ITypeConverter TypeConverter { get; set; }

		/// <summary>
		/// Gets or sets the type converter options.
		/// </summary>
		public virtual TypeConverterOptions TypeConverterOptions
		{
			get { return typeConverterOptions; }
		}

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
		/// the default value was explicitly set. True if it was
		/// explicitly set, otherwise false.
		/// </summary>
		public virtual bool IsDefaultSet { get; set; }

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
#endif // !NET_2_0
