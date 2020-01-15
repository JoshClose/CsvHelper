// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public class CsvConfiguration : IReaderConfiguration, IWriterConfiguration
	{
		private string delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
		private char escape = '"';
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
		private readonly ClassMapCollection maps;
		private NewLine newLine;

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsCache"/>.
		/// </summary>
		public virtual TypeConverterOptionsCache TypeConverterOptionsCache { get; set; } = new TypeConverterOptionsCache();

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsCache"/>.
		/// </summary>
		public virtual TypeConverterCache TypeConverterCache { get; set; } = new TypeConverterCache();

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		public virtual bool HasHeaderRecord { get; set; } = true;

		/// <summary>
		/// Gets or sets the function that is called when a header validation check is ran. The default function
		/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
		/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
		/// Arguments: isValid, headerNames, headerNameIndex, context
		/// </summary>
		public virtual Action<bool, string[], int, ReadingContext> HeaderValidated { get; set; } = ConfigurationFunctions.HeaderValidated;

		/// <summary>
		/// Gets or sets the function that is called when a missing field is found. The default function will
		/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
		/// like logging the issue instead of throwing an exception.
		/// Arguments: headerNames, index, context
		/// </summary>
		public virtual Action<string[], int, ReadingContext> MissingFieldFound { get; set; } = ConfigurationFunctions.MissingFieldFound;

		/// <summary>
		/// Gets or sets the function that is called when bad field data is found. A field
		/// has bad data if it contains a quote and the field is not quoted (escaped).
		/// You can supply your own function to do other things like logging the issue
		/// instead of throwing an exception.
		/// Arguments: context
		/// </summary>
		public virtual Action<ReadingContext> BadDataFound { get; set; } = ConfigurationFunctions.BadDataFound;

		/// <summary>
		/// Gets or sets the function that is called when a reading exception occurs.
		/// The default function will re-throw the given exception. If you want to ignore
		/// reading exceptions, you can supply your own function to do other things like
		/// logging the issue.
		/// Arguments: exception
		/// </summary>
		public virtual Func<CsvHelperException, bool> ReadingExceptionOccurred { get; set; } = ConfigurationFunctions.ReadingExceptionOccurred;

		/// <summary>
		/// Gets or sets the callback that will be called to
		/// determine whether to skip the given record or not.
		/// </summary>
		public virtual Func<string[], bool> ShouldSkipRecord { get; set; } = ConfigurationFunctions.ShouldSkipRecord;

		/// <summary>
		/// Gets or sets a value indicating if a line break found in a quote field should
		/// be considered bad data. True to consider a line break bad data, otherwise false.
		/// Defaults to false.
		/// </summary>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if fields should be sanitized
		/// to prevent malicious injection. This covers MS Excel, 
		/// Google Sheets and Open Office Calc.
		/// </summary>
		public virtual bool SanitizeForInjection { get; set; }

		/// <summary>
		/// Gets or sets the characters that are used for injection attacks.
		/// </summary>
		public virtual char[] InjectionCharacters { get; set; } = new[] { '=', '@', '+', '-' };

		/// <summary>
		/// Gets or sets the character used to escape a detected injection.
		/// </summary>
		public virtual char InjectionEscapeCharacter { get; set; } = '\t';

		/// <summary>
		/// Gets or sets a value indicating whether changes in the column
		/// count should be detected. If true, a <see cref="BadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		/// <value>
		/// <c>true</c> if [detect column count changes]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool DetectColumnCountChanges { get; set; }

		/// <summary>
		/// Prepares the header field for matching against a member name.
		/// The header field and the member name are both ran through this function.
		/// You should do things like trimming, removing whitespace, removing underscores,
		/// and making casing changes to ignore case.
		/// </summary>
		public virtual Func<string, int, string> PrepareHeaderForMatch { get; set; } = ConfigurationFunctions.PrepareHeaderForMatch;

		/// <summary>
		/// Determines if constructor parameters should be used to create
		/// the class instead of the default constructor and members.
		/// </summary>
		public virtual Func<Type, bool> ShouldUseConstructorParameters { get; set; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <summary>
		/// Chooses the constructor to use for constructor mapping.
		/// </summary>
		public virtual Func<Type, ConstructorInfo> GetConstructor { get; set; } = ConfigurationFunctions.GetConstructor;

		/// <summary>
		/// Gets or sets the comparer used to order the properties
		/// of dynamic objects when writing. The default is null,
		/// which will preserve the order the object properties
		/// were created with.
		/// </summary>
		public virtual IComparer<string> DynamicPropertySort { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		public virtual bool IgnoreReferences { get; set; }

		/// <summary>
		/// Gets or sets the field trimming options.
		/// </summary>
		public virtual TrimOptions TrimOptions { get; set; }

		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is CultureInfo.TextInfo.ListSeparator.
		/// </summary>
		public virtual string Delimiter
		{
			get { return delimiter; }
			set
			{
				if (value == "\n")
				{
					throw new ConfigurationException("Newline is not a valid delimiter.");
				}

				if (value == "\r")
				{
					throw new ConfigurationException("Carriage return is not a valid delimiter.");
				}

				if (value == Convert.ToString(quote))
				{
					throw new ConfigurationException("You can not use the quote as a delimiter.");
				}

				delimiter = value;
			}
		}

		/// <summary>
		/// Gets or sets the escape character used to escape a quote inside a field.
		/// Default is '"'.
		/// </summary>
		public virtual char Escape
		{
			get { return escape; }
			set
			{
				if (value == '\n')
				{
					throw new ConfigurationException("Newline is not a valid escape.");
				}

				if (value == '\r')
				{
					throw new ConfigurationException("Carriage return is not a valid escape.");
				}

				if (value.ToString() == delimiter)
				{
					throw new ConfigurationException("You can not use the delimiter as an escape.");
				}

				escape = value;

				doubleQuoteString = escape + quoteString;
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
				if (value == '\n')
				{
					throw new ConfigurationException("Newline is not a valid quote.");
				}

				if (value == '\r')
				{
					throw new ConfigurationException("Carriage return is not a valid quote.");
				}

				if (value == '\0')
				{
					throw new ConfigurationException("Null is not a valid quote.");
				}

				if (Convert.ToString(value) == delimiter)
				{
					throw new ConfigurationException("You can not use the delimiter as a quote.");
				}

				quote = value;

				quoteString = Convert.ToString(value, cultureInfo);
				doubleQuoteString = escape + quoteString;
			}
		}

		/// <summary>
		/// Gets a string representation of the currently configured Quote character.
		/// </summary>
		/// <value>
		/// The new quote string.
		/// </value>
		public virtual string QuoteString => quoteString;

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		public virtual string DoubleQuoteString => doubleQuoteString;

		/// <summary>
		/// Gets or sets a function that is used to determine if a field should get quoted
		/// when writing.
		/// Arguments: field, context
		/// </summary>
		public Func<string, WritingContext, bool> ShouldQuote { get; set; } = ConfigurationFunctions.ShouldQuote;

		/// <summary>
		/// Gets or sets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		public virtual char Comment { get; set; } = '#';

		/// <summary>
		/// Gets or sets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		public virtual bool AllowComments { get; set; }

		/// <summary>
		/// Gets or sets the size of the buffer
		/// used for reading CSV files.
		/// Default is 2048.
		/// </summary>
		public virtual int BufferSize { get; set; } = 2048;

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
		public virtual Encoding Encoding { get; set; } = Encoding.UTF8;

		/// <summary>
		/// Gets or sets the culture info used to read an write CSV files.
		/// Default is <see cref="System.Globalization.CultureInfo.CurrentCulture"/>.
		/// </summary>
		public virtual CultureInfo CultureInfo
		{
			get { return cultureInfo; }
			set { cultureInfo = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if quotes should be
		/// ignored when parsing and treated like any other character.
		/// </summary>
		public virtual bool IgnoreQuotes { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if private
		/// member should be read from and written to.
		/// True to include private member, otherwise false. Default is false.
		/// </summary>
		public virtual bool IncludePrivateMembers { get; set; }

		/// <summary>
		/// Gets or sets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		public virtual MemberTypes MemberTypes { get; set; } = MemberTypes.Properties;

		/// <summary>
		/// Gets or sets a value indicating if blank lines
		/// should be ignored when reading.
		/// True to ignore, otherwise false. Default is true.
		/// </summary>
		public virtual bool IgnoreBlankLines { get; set; } = true;

		/// <summary>
		/// Gets or sets a callback that will return the prefix for a reference header.
		/// Arguments: memberType, memberName
		/// </summary>
		public virtual Func<Type, string, string> ReferenceHeaderPrefix { get; set; }

		/// <summary>
		/// The configured <see cref="ClassMap"/>s.
		/// </summary>
		public virtual ClassMapCollection Maps => maps;

		/// <summary>
		/// Gets or sets the newline to use when writing.
		/// </summary>
		public virtual NewLine NewLine
		{
			get => newLine;
			set
			{
				newLine = value;

				switch (value)
				{
					case NewLine.CR:
						NewLineString = NewLines.CR;
						break;
					case NewLine.LF:
						NewLineString = NewLines.LF;
						break;
					case NewLine.Environment:
						NewLineString = Environment.NewLine;
						break;
					default:
						NewLineString = NewLines.CRLF;
						break;
				}
			}
		}

		/// <summary>
		/// Gets the newline string to use when writing. This string is determined
		/// by the <see cref="NewLine"/> value.
		/// </summary>
		public virtual string NewLineString { get; protected set; }

		/// <summary>
		/// Gets or sets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		public virtual bool UseNewObjectForNullReferenceMembers { get; set; } = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvConfiguration"/> class
		/// using the given <see cref="System.Globalization.CultureInfo"/>. Since <see cref="Delimiter"/>
		/// uses <see cref="CultureInfo"/> for it's default, the given <see cref="System.Globalization.CultureInfo"/>
		/// will be used instead.
		/// </summary>
		/// <param name="cultureInfo">The culture information.</param>
		public CsvConfiguration(CultureInfo cultureInfo)
		{
			maps = new ClassMapCollection(this);
			this.cultureInfo = cultureInfo;
			delimiter = cultureInfo.TextInfo.ListSeparator;
			NewLine = cultureInfo == CultureInfo.InvariantCulture ? NewLine.CRLF : NewLine.Environment;
		}

		/// <summary>
		/// Use a <see cref="ClassMap{T}" /> to configure mappings.
		/// When using a class map, no members are mapped by default.
		/// Only member specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		public virtual TMap RegisterClassMap<TMap>() where TMap : ClassMap
		{
			var map = ReflectionHelper.CreateInstance<TMap>();
			RegisterClassMap(map);

			return map;
		}

		/// <summary>
		/// Use a <see cref="ClassMap{T}" /> to configure mappings.
		/// When using a class map, no members are mapped by default.
		/// Only members specified in the mapping are used.
		/// </summary>
		/// <param name="classMapType">The type of mapping class to use.</param>
		public virtual ClassMap RegisterClassMap(Type classMapType)
		{
			if (!typeof(ClassMap).IsAssignableFrom(classMapType))
			{
				throw new ArgumentException("The class map type must inherit from CsvClassMap.");
			}

			var map = (ClassMap)ReflectionHelper.CreateInstance(classMapType);
			RegisterClassMap(map);

			return map;
		}

		/// <summary>
		/// Registers the class map.
		/// </summary>
		/// <param name="map">The class map to register.</param>
		public virtual void RegisterClassMap(ClassMap map)
		{
			if (map.MemberMaps.Count == 0 && map.ReferenceMaps.Count == 0)
			{
				throw new ConfigurationException("No mappings were specified in the CsvClassMap.");
			}

			Maps.Add(map);
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <typeparam name="TMap">The map type to unregister.</typeparam>
		public virtual void UnregisterClassMap<TMap>()
			where TMap : ClassMap
		{
			UnregisterClassMap(typeof(TMap));
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <param name="classMapType">The map type to unregister.</param>
		public virtual void UnregisterClassMap(Type classMapType)
		{
			maps.Remove(classMapType);
		}

		/// <summary>
		/// Unregisters all class maps.
		/// </summary>
		public virtual void UnregisterClassMap()
		{
			maps.Clear();
		}

		/// <summary>
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <typeparam name="T">The type to generate the map for.</typeparam>
		/// <returns>The generate map.</returns>
		public virtual ClassMap<T> AutoMap<T>()
		{
			var map = ReflectionHelper.CreateInstance<DefaultClassMap<T>>();
			map.AutoMap(this);
			maps.Add(map);

			return map;
		}

		/// <summary>
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <param name="type">The type to generate for the map.</param>
		/// <returns>The generate map.</returns>
		public virtual ClassMap AutoMap(Type type)
		{
			var mapType = typeof(DefaultClassMap<>).MakeGenericType(type);
			var map = (ClassMap)ReflectionHelper.CreateInstance(mapType);
			map.AutoMap(this);
			maps.Add(map);

			return map;
		}
	}
}
