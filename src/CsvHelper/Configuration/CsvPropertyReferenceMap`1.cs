using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mappinging info for a reference property mapping to a class.
	/// </summary>
	/// <typeparam name="TClassMap">The type of the class map.</typeparam>
	public class CsvPropertyReferenceMap<TClassMap> : CsvPropertyReferenceMap 
		where TClassMap : CsvClassMap
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap&lt;TClassMap&gt;"/> class.
		/// </summary>
		/// <param name="property">The property.</param>
		public CsvPropertyReferenceMap( PropertyInfo property ) : base( property )
		{
			var map = Activator.CreateInstance<TClassMap>();
			ReferenceProperties = map.PropertyMaps;
		}
	}
}
