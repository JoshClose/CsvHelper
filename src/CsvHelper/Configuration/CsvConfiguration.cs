// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
#if !NET_2_0
using System.Linq;
using System.Linq.Expressions;
#endif
using System.Reflection;
using System.Text;
#if WINRT_4_5
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public class CsvConfiguration
	{
#if !NET_2_0
		private CsvPropertyMapCollection properties = new CsvPropertyMapCollection();
		private List<CsvPropertyReferenceMap> references = new List<CsvPropertyReferenceMap>();
#endif
#if !WINRT_4_5
		private BindingFlags propertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;
#endif
#if WINRT_4_5
#endif
		private bool hasHeaderRecord = true;
		private bool isStrictMode = true;
		private string delimiter = ",";
		private char quote = '"';
		private char comment = '#';
		private int bufferSize = 2048;
		private bool isCaseSensitive = true;
		private Encoding encoding = Encoding.UTF8;
		private bool quoteAllFields = false;
		private bool quoteNoFields = false;
        private bool isIgnoreSpacesInHeader = false;

#if !NET_2_0
		/// <summary>
		/// Gets the constructor expression.
		/// </summary>
		public virtual NewExpression Constructor { get; private set; } 

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
#endif

#if !WINRT_4_5
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
#endif

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
		/// Gets or sets a value indicating whether changes in the column
		/// count should be detected. If true, a <see cref="CsvBadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		/// <value>
		/// <c>true</c> if [detect column count changes]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool DetectColumnCountChanges { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether matching header
		/// column names is case sensitive. True for case sensitive
		/// matching, otherwise false.
		/// </summary>
		public virtual bool IsCaseSensitive
		{
			get { return isCaseSensitive; }
			set { isCaseSensitive = value; }
		}

		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		public virtual string Delimiter
		{
			get { return delimiter; }
			set
			{
				if( value == "\n" )
				{
					throw new CsvHelperException( "Newline is not a valid delimiter." );
				}
				if( value == "\r" )
				{
					throw new CsvHelperException( "Carriage return is not a valid delimiter." );
				}
				if( value == "\0" )
				{
					throw new CsvHelperException( "Null is not a valid delimiter." );
				}
				if( value == Convert.ToString( quote ) )
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
				if( Convert.ToString( value ) == delimiter )
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
		/// Gets or sets a value indicating whether all fields are quoted when writing,
		/// or just ones that have to be. <see cref="QuoteAllFields"/> and
		/// <see cref="QuoteNoFields"/> cannot be true at the same time. Turning one
		/// on will turn the other off.
		/// </summary>
		/// <value>
		///   <c>true</c> if all fields should be quoted; otherwise, <c>false</c>.
		/// </value>
		public virtual bool QuoteAllFields
		{
			get { return quoteAllFields; }
			set
			{
				quoteAllFields = value;
				if( quoteAllFields && quoteNoFields )
				{
					// Both can't be true at the same time.
					quoteNoFields = false;
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether no fields are quoted when writing.
		/// <see cref="QuoteAllFields"/> and <see cref="QuoteNoFields"/> cannot be true 
		/// at the same time. Turning one on will turn the other off.
		/// </summary>
		/// <value>
		///   <c>true</c> if [quote no fields]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool QuoteNoFields
		{
			get { return quoteNoFields; }
			set
			{
				quoteNoFields = value;
				if( quoteNoFields && quoteAllFields )
				{
					// Both can't be true at the same time.
					quoteAllFields = false;
				}
			}
		}

        /// <summary>
        /// Gets or sets a value indicating whether matching header
		/// column names should ignore spaces. True for ignoring space
		/// matching, otherwise false.
        /// </summary>

        public virtual bool IsIgnoreSpaceInHeader
        {
            get { return isIgnoreSpacesInHeader; }
            set { isIgnoreSpacesInHeader = value; }
        }

		/// <summary>
		/// Gets or sets a value indicating whether the number of bytes should
		/// be counted while parsing. Default is false. This will slow down parsing
		/// because it needs to get the byte count of every char for the given encoding.
		/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
		/// </summary>
		public virtual bool CountBytes { get; set; }

		/// <summary>
		/// Gets or sets the encoding used when counting bytes.
		/// </summary>
		public virtual Encoding Encoding
		{
			get { return encoding; }
			set { encoding = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether empty rows should be skipped when reading.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <value>
		///   <c>true</c> if [skip empty rows]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool SkipEmptyRecords { get; set; }

#if !NET_2_0
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
			Constructor = classMap.Constructor;
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
			AttributeMapping( typeof( TClass ) );
		}

		/// <summary>
		/// Use <see cref="CsvFieldAttribute"/>s to configure mappings.
		/// All properties are mapped by default and attribute mapping 
		/// will change the default property behavior.
		/// </summary>
		/// <param name="type">The type of custom class that contains the attributes.</param>
		public virtual void AttributeMapping( Type type )
		{
			var props = type.GetProperties();

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
							.Index( csvFieldAttribute.Index )
							.Format( csvFieldAttribute.Format );
						if( csvFieldAttribute.Name != null )
						{
							map.Name( csvFieldAttribute.Name );
						}
						else if( csvFieldAttribute.Names != null && csvFieldAttribute.Names.Length > 0 )
						{
							map.Name( csvFieldAttribute.Names );
						}
						if( csvFieldAttribute.DefaultIsSet )
						{
							map.Default( csvFieldAttribute.Default );
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
					var refProps = property.PropertyType.GetProperties();
					foreach( var refProp in refProps )
					{
						var refCsvFieldAttributes = ReflectionHelper.GetAttributes<CsvFieldAttribute>( refProp, true );
						var refCsvFieldAttribute = refCsvFieldAttributes.FirstOrDefault( a => a.ReferenceKey == csvFieldAttribute.ReferenceKey );
						CsvPropertyMap map;
						if( refCsvFieldAttribute != null )
						{
							map = PropertyMap( refProp )
								.Ignore( refCsvFieldAttribute.Ignore )
								.Index( refCsvFieldAttribute.Index )
								.Format( refCsvFieldAttribute.Format );
							if( refCsvFieldAttribute.Name != null )
							{
								map.Name( refCsvFieldAttribute.Name );
							}
							else if( refCsvFieldAttribute.Names != null && refCsvFieldAttribute.Names.Length > 0 )
							{
								map.Name( refCsvFieldAttribute.Names );
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
#endif
	}
}
