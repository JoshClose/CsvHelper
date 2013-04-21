// Copyright 2009-2013 Josh Close
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
		private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
		private bool quoteAllFields = false;
		private bool quoteNoFields = false;

#if !NET_2_0
		/// <summary>
		/// Gets or sets the mapping.
		/// </summary>
		public virtual CsvClassMap Mapping { get; set; }
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

#if !NET_2_0
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
			var mapping = ReflectionHelper.CreateInstance<TMap>();
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
			var mapping = ReflectionHelper.CreateInstance<TMap>();
			ClassMapping( mapping );
		}

		/// <summary>
		/// Use a <see cref="CsvClassMap"/> instance to configure mappings.
		/// When using a class map, no properties are mapped by default.
		/// Only properties specified in the mapping are used.
		/// </summary>
		public virtual void ClassMapping( CsvClassMap classMap )
		{
			if( classMap.Constructor == null && classMap.PropertyMaps.Count == 0 && classMap.ReferenceMaps.Count == 0 )
			{
				throw new CsvConfigurationException( "No mappings were specified in the CsvClassMap." );
			}

			Mapping = classMap;
		}
#endif
	}
}
