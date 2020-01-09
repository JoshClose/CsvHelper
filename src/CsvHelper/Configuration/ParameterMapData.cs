// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.TypeConversion;
using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// The constructor parameter data for the map.
	/// </summary>
	[DebuggerDisplay( "Index = {Index}, Name = {Name}, Parameter = {Parameter}" )]
	public class ParameterMapData
	{
		/// <summary>
		/// Gets the <see cref="ParameterInfo"/> that the data
		/// is associated with.
		/// </summary>
		public virtual ParameterInfo Parameter { get; private set; }

		/// <summary>
		/// Gets or sets the type converter.
		/// </summary>
		public virtual ITypeConverter TypeConverter { get; set; }

		/// <summary>
		/// Gets or sets the type converter options.
		/// </summary>
		public virtual TypeConverterOptions TypeConverterOptions { get; set; } = new TypeConverterOptions();

		/// <summary>
		/// Gets or sets the column index.
		/// </summary>
		public virtual int Index { get; set; } = -1;

		/// <summary>
		/// Gets or sets the column name.
		/// </summary>
		public virtual string Name { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterMapData"/> class.
		/// </summary>
		/// <param name="parameter">The constructor parameter.</param>
		public ParameterMapData( ParameterInfo parameter )
		{
			Parameter = parameter;
		}
	}
}
