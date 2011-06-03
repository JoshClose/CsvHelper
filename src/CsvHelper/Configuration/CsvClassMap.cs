using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Maps class properties to CSV fields.
	/// </summary>
	/// <typeparam name="T">The <see cref="Type"/> of class to map.</typeparam>
	public abstract class CsvClassMap<T>
	{
		private readonly List<CsvPropertyMap> properties = new List<CsvPropertyMap>();

		/// <summary>
		/// The class property mappings.
		/// </summary>
		public ReadOnlyCollection<CsvPropertyMap> Properties { get { return properties.AsReadOnly(); } }

		/// <summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		protected CsvPropertyMap Map( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );
			var propertyMap = new CsvPropertyMap( property );
			properties.Add( propertyMap );
			return propertyMap;
		}
	}
}
