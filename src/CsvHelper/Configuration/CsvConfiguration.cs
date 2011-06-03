using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public class CsvConfiguration
	{
		private List<CsvPropertyMap> properties = new List<CsvPropertyMap>();
		private BindingFlags propertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;
		private bool hasHeaderRecord = true;
		private char delimiter = ',';
		private char quote = '"';

		/// <summary>
		/// Gets the property mappings.
		/// </summary>
		public ReadOnlyCollection<CsvPropertyMap> Properties { get { return properties.AsReadOnly(); } }

		/// <summary>
		/// Gets or sets the property binding flags.
		/// This determines what properties on the custom
		/// class are used. Default is Public | Instance.
		/// </summary>
		public BindingFlags PropertyBindingFlags
		{
			get { return propertyBindingFlags; }
			set { propertyBindingFlags = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		public bool HasHeaderRecord
		{
			get { return hasHeaderRecord; } 
			set { hasHeaderRecord = value; }
		}

		/// <summary>
		/// Gets or sets the strict reading flag.
		/// True to enable strict reading, otherwise false.
		/// Strict reading will cause a <see cref="MissingFieldException" />
		/// to be thrown if a named index is not found.
		/// </summary>
		public bool Strict { get; set; }

		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// </summary>
		public char Delimiter
		{
			get { return delimiter; }
			set { delimiter = value; }
		}

		/// <summary>
		/// Gets or sets the quote used to quote fields.
		/// </summary>
		public char Quote
		{
			get { return quote; }
			set { quote = value; }
		}

		/// <summary>
		/// Maps a property of a class to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		public CsvPropertyMap PropertyMap<T>( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );
			return PropertyMap( property );
		}

		/// <summary>
		/// Maps a property of a class to a CSV field.
		/// </summary>
		/// <param name="property">The property to map.</param>
		public CsvPropertyMap PropertyMap( PropertyInfo property )
		{
			return new CsvPropertyMap( property );
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap{T}"/> to configure mappings.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		/// <typeparam name="TClass">The type of custom class that is being mapped.</typeparam>
		public void ClassMapping<TMap, TClass>() where TMap : CsvClassMap<TClass>
		{
			var mapping = Activator.CreateInstance<TMap>() as CsvClassMap<TClass>;
			properties = mapping.Properties.ToList();
		}

		/// <summary>
		/// Use <see cref="CsvFieldAttribute"/>s to configure mappings.
		/// </summary>
		/// <typeparam name="TClass">The type of custom class that contains the attributes.</typeparam>
		public void AttributeMapping<TClass>() where TClass : class
		{
// ReSharper disable LocalVariableHidesMember
			var properties = typeof( TClass ).GetProperties( PropertyBindingFlags );
// ReSharper restore LocalVariableHidesMember
			foreach( var property in properties )
			{
				var csvFieldAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( property, true );
				PropertyMap( property )
					.Ignore( csvFieldAttribute.Ignore )
					.Index( csvFieldAttribute.FieldIndex )
					.Name( csvFieldAttribute.FieldName )
					.TypeConverter( ReflectionHelper.GetTypeConverter( property ) );
			}
		}
	}
}
