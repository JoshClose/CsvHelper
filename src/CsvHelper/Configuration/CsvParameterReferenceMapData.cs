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
	/// The configuration data for the reference map.
	/// </summary>
	[DebuggerDisplay( "Prefix = {Prefix}, Parameter = {Parameter}" )]
    public class CsvParameterReferenceMapData
    {
		private string prefix;

		/// <summary>
		/// Gets or sets the header prefix to use.
		/// </summary>
		public virtual string Prefix
		{
			get { return prefix; }
			set
			{
				prefix = value;
				foreach( var propertyMap in Mapping.PropertyMaps )
				{
					propertyMap.Data.Names.Prefix = value;
				}
			}
		}

		/// <summary>
		/// Gets the <see cref="ParameterInfo"/> that the data
		/// is associated with.
		/// </summary>
		public virtual ParameterInfo Parameter { get; private set; }

		/// <summary>
		/// Gets the mapping this is a reference for.
		/// </summary>
		public CsvClassMap Mapping { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvParameterReferenceMapData"/> class.
		/// </summary>
		/// <param name="parameter">The parameter.</param>
		/// <param name="mapping">The mapping this is a reference for.</param>
		public CsvParameterReferenceMapData( ParameterInfo parameter, CsvClassMap mapping )
		{
			Parameter = parameter;
			Mapping = mapping;
		}
	}
}
