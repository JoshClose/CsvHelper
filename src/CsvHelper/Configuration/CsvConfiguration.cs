// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
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
		private CsvPropertyMapCollection properties = new CsvPropertyMapCollection();
		private List<CsvPropertyReferenceMap> references = new List<CsvPropertyReferenceMap>();
		private BindingFlags propertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;
		private bool hasHeaderRecord = true;
		private bool isStrictMode = true;
		private char delimiter = ',';
		private char quote = '"';
		private char comment = '#';
		private int bufferSize = 2048;

		/// <summary>
		/// Gets the property mappings.
		/// </summary>
		public virtual CsvPropertyMapCollection Properties
		{
			get { return properties; }
		}

		/// <summary>
		/// Gets the reference mappings.
		/// </summary>
		public virtual List<CsvPropertyReferenceMap> References
		{
			get { return references; }
		}

		/// <summary>
		/// Gets or sets the property binding flags.
		/// This determines what properties on the custom
		/// class are used. Default is Public | Instance.
		/// </summary>
		public virtual BindingFlags PropertyBindingFlags
		{
			get { return propertyBindingFlags; }
			set { propertyBindingFlags = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		public virtual bool HasHeaderRecord
		{
			get { return hasHeaderRecord; }
			set { hasHeaderRecord = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if strict reading is enabled.
		/// True to enable strict reading, otherwise false.
		/// Strict reading will cause a <see cref="CsvMissingFieldException" />
		/// to be thrown if a named index is not found.
		/// </summary>
		public virtual bool IsStrictMode
		{
			get { return isStrictMode; }
			set { isStrictMode = value; }
		}

		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		public virtual char Delimiter
		{
			get { return delimiter; }
			set
			{
				if( value == '\n' )
				{
					throw new CsvHelperException( "Newline is not a valid delimiter." );
				}
				if( value == '\r' )
				{
					throw new CsvHelperException( "Carriage return is not a valid delimiter." );
				}
				if( value == '\0' )
				{
					throw new CsvHelperException( "Null is not a valid delimiter." );
				}
				if( value == quote )
				{
					throw new CsvHelperException( "You can not use the quote as a delimiter." );
				}
				delimiter = value;
			}
		}

		/// <summary>
		/// Gets or sets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		public virtual char Quote
		{
			get { return quote; }
			set
			{
				if( value == '\n' )
				{
					throw new CsvHelperException( "Newline is not a valid quote." );
				}
				if( value == '\r' )
				{
					throw new CsvHelperException( "Carriage return is not a valid quote." );
				}
				if( value == '\0' )
				{
					throw new CsvHelperException( "Null is not a valid quote." );
				}
				if( value == delimiter )
				{
					throw new CsvHelperException( "You can not use the delimiter as a quote." );
				}
				quote = value;
			}
		}

		/// <summary>
		/// Gets or sets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		public virtual char Comment
		{
			get { return comment; }
			set { comment = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		public virtual bool AllowComments { get; set; }

		/// <summary>
		/// Gets or sets the size of the buffer
		/// used for reading and writing CSV files.
		/// Default is 2048.
		/// </summary>
		public virtual int BufferSize
		{
			get { return bufferSize; }
			set { bufferSize = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if InvariantCulture
		/// should be used when reading and writing. True to
		/// use InvariantCulture, false to use CurrentCulture.
		/// </summary>
		public virtual bool UseInvariantCulture { get; set; }

		/// <summary>
		/// Gets or sets the number of fields the CSV file has.
		/// If this is known ahead of time, set
		/// to make parsing more efficient.
		/// </summary>
		public virtual int FieldCount { get; set; }

		/// <summary>
		/// Maps a property of a class to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		public virtual CsvPropertyMap PropertyMap<T>( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );
			return PropertyMap( property );
		}

		/// <summary>
		/// Maps a property of a class to a CSV field.
		/// </summary>
		/// <param name="property">The property to map.</param>
		public virtual CsvPropertyMap PropertyMap( PropertyInfo property )
		{
			return new CsvPropertyMap( property );
		}

		/// <summary>
		/// Maps a property of a class to another mapped class.
		/// </summary>
		/// <param name="expression">The expression.</param>
		/// <returns></returns>
		public virtual CsvPropertyReferenceMap ReferenceMap<T>( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );
			return new CsvPropertyReferenceMap( property );
		}

		/// <summary>
		/// Maps a property of a class to another mapped class.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <returns></returns>
		public virtual CsvPropertyReferenceMap ReferenceMap( PropertyInfo property )
		{
			return new CsvPropertyReferenceMap( property );
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap{T}"/> to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		/// <typeparam name="TClass">The type of custom class that is being mapped.</typeparam>
		public virtual void ClassMapping<TMap, TClass>()
			where TMap : CsvClassMap<TClass>
			where TClass : class
		{
			var mapping = Activator.CreateInstance<TMap>() as CsvClassMap<TClass>;
			ClassMapping( mapping );
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap{T}"/> to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		public virtual void ClassMapping<TMap>() where TMap : CsvClassMap
		{
			var mapping = Activator.CreateInstance<TMap>();
			ClassMapping( mapping );
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap"/> instance to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		public virtual void ClassMapping( CsvClassMap classMap )
		{
			properties = classMap.PropertyMaps;
			references = classMap.ReferenceMaps;
		}

		/// <summary>
		/// Use <see cref="CsvFieldAttribute"/>s to configure mappings.
		/// All properties are mapped by default and attribute mapping 
		/// will change the default property behavior.
		/// </summary>
		/// <typeparam name="TClass">The type of custom class that contains the attributes.</typeparam>
		public virtual void AttributeMapping<TClass>() where TClass : class
		{
			var props = typeof( TClass ).GetProperties( PropertyBindingFlags );
			foreach( var property in props )
			{
				var csvFieldAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( property, true );
				if( csvFieldAttribute == null || csvFieldAttribute.ReferenceKey == null )
				{
					// This is a property map.
					CsvPropertyMap map;
					if( csvFieldAttribute != null )
					{
						map = PropertyMap( property )
							.Ignore( csvFieldAttribute.Ignore )
							.Index( csvFieldAttribute.Index );
						if( csvFieldAttribute.Name != null )
						{
							map.Name( csvFieldAttribute.Name );
						}
					}
					else
					{
						// Use defaults.
						map = PropertyMap( property );
					}
					var typeConverter = ReflectionHelper.GetTypeConverterFromAttribute( property );
					if( typeConverter != null )
					{
						map.TypeConverter( typeConverter );
					}
					properties.Add( map );
				}
				else
				{
					// This is a reference mapping.
					var refMap = ReferenceMap( property );
					references.Add( refMap );
					var refProps = property.PropertyType.GetProperties( PropertyBindingFlags );
					foreach( var refProp in refProps )
					{
						var refCsvFieldAttributes = ReflectionHelper.GetAttributes<CsvFieldAttribute>( refProp, true );
						var refCsvFieldAttribute = refCsvFieldAttributes.FirstOrDefault( a => a.ReferenceKey == csvFieldAttribute.ReferenceKey );
						CsvPropertyMap map;
						if( refCsvFieldAttribute != null )
						{
							map = PropertyMap( refProp )
								.Ignore( refCsvFieldAttribute.Ignore )
								.Index( refCsvFieldAttribute.Index );
							if( refCsvFieldAttribute.Name != null )
							{
								map.Name( refCsvFieldAttribute.Name );
							}
						}
						else
						{
							// Use defaults.
							map = PropertyMap( refProp );
						}
						var typeConverter = ReflectionHelper.GetTypeConverterFromAttribute( refProp );
						if( typeConverter != null )
						{
							map.TypeConverter( typeConverter );
						}
						refMap.ReferenceProperties.Add( map );
					}
				}
			}
		}
	}
}
