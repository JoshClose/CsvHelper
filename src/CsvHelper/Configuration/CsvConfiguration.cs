// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public class CsvConfiguration
	{
		private BindingFlags propertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;
		private bool hasHeaderRecord = true;
		private bool willThrowOnMissingField = true;
		private string delimiter = ",";
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private char[] quoteRequiredChars;
		private char comment = '#';
		private int bufferSize = 2048;
		private bool isHeaderCaseSensitive = true;
		private Encoding encoding = Encoding.UTF8;
		private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
		private bool quoteAllFields;
		private bool quoteNoFields;
		private bool ignoreBlankLines = true;
#if !NET_2_0
		private bool useNewObjectForNullReferenceProperties = true;
		private readonly CsvClassMapCollection maps = new CsvClassMapCollection();
#endif

#if !NET_2_0
		/// <summary>
		/// The configured <see cref="CsvClassMap"/>s.
		/// </summary>
		public virtual CsvClassMapCollection Maps
		{
			get { return maps; }
		}
#endif

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
		/// Gets or sets a value indicating the if the CSV
		/// file contains the Excel "sep=delimeter" config
		/// option in the first row.
		/// </summary>
		public virtual bool HasExcelSeparator { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if an exception will be
		/// thrown if a field defined in a mapping is missing.
		/// True to throw an exception, otherwise false.
		/// Default is true.
		/// </summary>
		public virtual bool WillThrowOnMissingField
		{
			get { return willThrowOnMissingField; }
			set { willThrowOnMissingField = value; }
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
		/// matching, otherwise false. Default is true.
		/// </summary>
		public virtual bool IsHeaderCaseSensitive
		{
			get { return isHeaderCaseSensitive; }
			set { isHeaderCaseSensitive = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether matcher header
		/// column names will ignore white space. True to ignore
		/// white space, otherwise false. Default is false.
		/// </summary>
		public virtual bool IgnoreHeaderWhiteSpace { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		public virtual bool IgnoreReferences { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether headers
		/// should be trimmed. True to trim headers,
		/// otherwise false. Default is false.
		/// </summary>
		public virtual bool TrimHeaders { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether fields
		/// should be trimmed. True to trim fields,
		/// otherwise false. Default is false.
		/// </summary>
		public virtual bool TrimFields { get; set; }

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
					throw new CsvConfigurationException( "Newline is not a valid delimiter." );
				}

				if( value == "\r" )
				{
					throw new CsvConfigurationException( "Carriage return is not a valid delimiter." );
				}

				if( value == "\0" )
				{
					throw new CsvConfigurationException( "Null is not a valid delimiter." );
				}

				if( value == Convert.ToString( quote ) )
				{
					throw new CsvConfigurationException( "You can not use the quote as a delimiter." );
				}

				delimiter = value;

				BuildRequiredQuoteChars();
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
					throw new CsvConfigurationException( "Newline is not a valid quote." );
				}

				if( value == '\r' )
				{
					throw new CsvConfigurationException( "Carriage return is not a valid quote." );
				}

				if( value == '\0' )
				{
					throw new CsvConfigurationException( "Null is not a valid quote." );
				}

				if( Convert.ToString( value ) == delimiter )
				{
					throw new CsvConfigurationException( "You can not use the delimiter as a quote." );
				}

				quote = value;

				quoteString = Convert.ToString( value, cultureInfo );
				doubleQuoteString = quoteString + quoteString;
			}
		}

		/// <summary>
		/// Gets a string representation of the currently configured Quote character.
		/// </summary>
		/// <value>
		/// The new quote string.
		/// </value>
		public virtual string QuoteString
		{
			get { return quoteString; }
		}

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		public virtual string DoubleQuoteString
		{
			get { return doubleQuoteString; }
		}

		/// <summary>
		/// Gets an array characters that require
		/// the field to be quoted.
		/// </summary>
		public virtual char[] QuoteRequiredChars
		{
			get { return quoteRequiredChars; }
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
		/// Gets or sets the culture info used to read an write CSV files.
		/// </summary>
		public virtual CultureInfo CultureInfo
		{
			get { return cultureInfo; }
			set { cultureInfo = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether empty rows should be skipped when reading.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <value>
		///   <c>true</c> if [skip empty rows]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool SkipEmptyRecords { get; set; }

		/// <summary>
		/// Gets or sets the callback that will be called to
		/// determine whether to skip the given record or not.
		/// This overrides the <see cref="SkipEmptyRecords"/> setting.
		/// </summary>
		public virtual Func<string[], bool> ShouldSkipRecord { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if quotes should be
		/// ingored when parsing and treated like any other character.
		/// </summary>
		public virtual bool IgnoreQuotes { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if private
		/// get and set property accessors should be
		/// ignored when reading and writing.
		/// True to ignore, otherwise false. Default is false.
		/// </summary>
		public virtual bool IgnorePrivateAccessor { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if blank lines
		/// should be ignored when reading.
		/// True to ignore, otherwise false. Default is true.
		/// </summary>
		public virtual bool IgnoreBlankLines
		{
			get { return ignoreBlankLines; }
			set { ignoreBlankLines = value; }
		}

        /// <summary>
        /// Gets or sets a value indicating if an Excel specific
        /// format should be used when writing fields containing
        /// numeric values. e.g. 00001 -> ="00001"
        /// </summary>
		public virtual bool UseExcelLeadingZerosFormatForNumerics { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if headers of reference
		/// properties should get prefixed by the parent property name 
		/// when automapping.
		/// True to prefix, otherwise false. Default is false.
		/// </summary>
		public virtual bool PrefixReferenceHeaders { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if an exception should
		/// be thrown when bad field data is detected.
		/// True to throw, otherwise false. Default is false.
		/// </summary>
		public virtual bool ThrowOnBadData { get; set; }

		/// <summary>
		/// Gets or sets a method that gets called when bad
		/// data is detected.
		/// </summary>
		public virtual Action<string> BadDataCallback { get; set; }

#if !NET_2_0
		/// <summary>
		/// Gets or sets a value indicating whether
		/// exceptions that occur duruing reading
		/// should be ignored. True to ignore exceptions,
		/// otherwise false. Default is false.
		/// This is only applicable when during
		/// <see cref="ICsvReaderRow.GetRecords{T}"/>.
		/// </summary>
		public virtual bool IgnoreReadingExceptions { get; set; }

		/// <summary>
		/// Gets or sets the callback that is called when a reading
		/// exception occurs. This will only happen when
		/// <see cref="IgnoreReadingExceptions"/> is true, and when
		/// calling <see cref="ICsvReaderRow.GetRecords{T}"/>.
		/// </summary>
		public virtual Action<Exception, ICsvReader> ReadingExceptionCallback { get; set; }

		/// <summary>
		/// Gets or sets a value indicating that during writing if a new 
		/// object should be created when a reference property is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference property's properties.
		/// </summary>
		public virtual bool UseNewObjectForNullReferenceProperties
		{
			get { return useNewObjectForNullReferenceProperties; }
			set { useNewObjectForNullReferenceProperties = value; }
		}
		
		/// <summary>
		/// Use a <see cref="CsvClassMap{T}" /> to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		public virtual TMap RegisterClassMap<TMap>() where TMap : CsvClassMap
		{
			var map = ReflectionHelper.CreateInstance<TMap>();
			RegisterClassMap( map );

			return map;
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap{T}" /> to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		/// <param name="classMapType">The type of mapping class to use.</param>
		public virtual CsvClassMap RegisterClassMap( Type classMapType )
		{
			if( !typeof( CsvClassMap ).IsAssignableFrom( classMapType ) )
			{
				throw new ArgumentException( "The class map type must inherit from CsvClassMap." );
			}

			var map = (CsvClassMap)ReflectionHelper.CreateInstance( classMapType );
			RegisterClassMap( map );

			return map;
		}

		/// <summary>
		/// Registers the class map.
		/// </summary>
		/// <param name="map">The class map to register.</param>
		public virtual void RegisterClassMap( CsvClassMap map )
		{
			map.CreateMap();

			if( map.Constructor == null && map.PropertyMaps.Count == 0 && map.ReferenceMaps.Count == 0 )
			{
				throw new CsvConfigurationException( "No mappings were specified in the CsvClassMap." );
			}

			Maps.Add( map );
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <typeparam name="TMap">The map type to unregister.</typeparam>
		public virtual void UnregisterClassMap<TMap>() 
			where TMap : CsvClassMap
		{
			UnregisterClassMap( typeof( TMap ) );
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <param name="classMapType">The map type to unregister.</param>
		public virtual void UnregisterClassMap( Type classMapType )
		{
			maps.Remove( classMapType );
		}

		/// <summary>
		/// Unregisters all class maps.
		/// </summary>
		public virtual void UnregisterClassMap()
		{
			maps.Clear();
		}

		/// <summary>
		/// Generates a <see cref="CsvClassMap"/> for the type.
		/// </summary>
		/// <typeparam name="T">The type to generate the map for.</typeparam>
		/// <returns>The generate map.</returns>
		public virtual CsvClassMap AutoMap<T>()
		{
			return AutoMap( typeof( T ) );
		}

		/// <summary>
		/// Generates a <see cref="CsvClassMap"/> for the type.
		/// </summary>
		/// <param name="type">The type to generate for the map.</param>
		/// <returns>The generate map.</returns>
		public virtual CsvClassMap AutoMap( Type type )
		{
			var mapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( type );
			var map = (CsvClassMap)ReflectionHelper.CreateInstance( mapType );
			map.AutoMap( IgnoreReferences, PrefixReferenceHeaders );

			return map;
		}
#endif

		/// <summary>
		/// Creates a new CsvConfiguration.
		/// </summary>
		public CsvConfiguration()
		{
			BuildRequiredQuoteChars();
		}

		/// <summary>
		/// Builds the values for the RequiredQuoteChars property.
		/// </summary>
		private void BuildRequiredQuoteChars()
		{
			quoteRequiredChars = delimiter.Length > 1 ? 
				new[] { '\r', '\n' } : 
				new[] { '\r', '\n', delimiter[0] };
		}
	}
}
