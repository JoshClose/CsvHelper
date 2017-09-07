using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// The constructor paramter data for the map.
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
