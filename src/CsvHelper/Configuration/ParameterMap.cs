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
	/// Mapping for a constructor parameter.
	/// This may contain value type data, a constructor type map,
	/// or a reference map, depending on the type of the parameter.
	/// </summary>
	[DebuggerDisplay( "Data = {Data}" )]
	public class ParameterMap
    {
		/// <summary>
		/// Gets the parameter map data.
		/// </summary>
		public virtual ParameterMapData Data { get; protected set; }

		/// <summary>
		/// Gets or sets the map for a constructor type.
		/// </summary>
		public virtual ClassMap ConstructorTypeMap { get; set; }

		/// <summary>
		/// Gets or sets the map for a reference type.
		/// </summary>
		public virtual ParameterReferenceMap ReferenceMap { get; set; }

		/// <summary>
		/// Creates an instance of <see cref="ParameterMap"/> using
		/// the given information.
		/// </summary>
		/// <param name="parameter">The parameter being mapped.</param>
		public ParameterMap( ParameterInfo parameter )
		{
			Data = new ParameterMapData( parameter );
		}

		internal int GetMaxIndex()
		{
			return ReferenceMap?.GetMaxIndex() ?? Data.Index;
		}
	}
}
