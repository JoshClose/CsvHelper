﻿// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
	public class Configuration : IReaderConfiguration, IWriterConfiguration
	{
		private string delimiter = ",";
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private char[] quoteRequiredChars;
		private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
		private bool quoteAllFields;
		private bool quoteNoFields;
		private readonly ClassMapCollection maps;

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
		public virtual Action<bool, string[], int, IReadingContext> HeaderValidated { get; set; } = ( isValid, headerNames, headerNameIndex, context ) =>
		{
			if( isValid )
			{
				return;
			}

			var message =
				$"Header matching ['{string.Join( "', '", headerNames )}'] names at index {headerNameIndex} was not found. " +
				$"If you are expecting some headers to be missing and want to ignore this validation, " +
				$"set the configuration {nameof( HeaderValidated )} to null. You can also change the " +
				$"functionality to do something else, like logging the issue.";

			throw new ValidationException( context, message );
		};

		/// <summary>
		/// Gets or sets the function that is called when a missing field is found. The default function will
		/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
		/// like logging the issue instead of throwing an exception.
		/// Arguments: headerNames, index, context
		/// </summary>
		public virtual Action<string[], int, IReadingContext> MissingFieldFound { get; set; } = ( headerNames, index, context ) =>
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof( MissingFieldFound )} to null.";

			if( headerNames != null && headerNames.Length > 0 )
			{
				throw new MissingFieldException( context, $"Field with names ['{string.Join( "', '", headerNames )}'] at index '{index}' does not exist. {messagePostfix}" );
			}

			throw new MissingFieldException( context, $"Field at index '{index}' does not exist. {messagePostfix}" );
		};

		/// <summary>
		/// Gets or sets the function that is called when bad field data is found. A field
		/// has bad data if it contains a quote and the field is not quoted (escaped).
		/// You can supply your own function to do other things like logging the issue
		/// instead of throwing an exception.
		/// Arguments: context
		/// </summary>
		public virtual Action<IReadingContext> BadDataFound { get; set; } = context =>
		{
			throw new BadDataException( context, $"You can ignore bad data by setting {nameof( BadDataFound )} to null." );
		};

		/// <summary>
		/// Gets or sets the function that is called when a reading exception occurs.
		/// The default function will re-throw the given exception. If you want to ignore
		/// reading exceptions, you can supply your own function to do other things like
		/// logging the issue.
		/// Arguments: exception
		/// </summary>
		public virtual Action<CsvHelperException> ReadingExceptionOccurred { get; set; } = exception => throw exception;

		/// <summary>
		/// Gets or sets the callback that will be called to
		/// determine whether to skip the given record or not.
		/// </summary>
		public virtual Func<string[], bool> ShouldSkipRecord { get; set; } = record => false;

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
		public virtual Func<string, string> PrepareHeaderForMatch { get; set; } = header => header;

        /// <summary>
        /// Gets or sets the callback that will determine header value for dynamic columns.
        /// Default is: `Field{i+1}`
        /// </summary>
        public virtual Func<int, string> DynamicHeaderValue { get; set; } = i => $"Field{i+1}";

        /// <summary>
        /// Determines if constructor parameters should be used to create
        /// the class instead of the default constructor and members.
        /// </summary>
        public virtual Func<Type, bool> ShouldUseConstructorParameters { get; set; } = type =>
				!type.HasParameterlessConstructor()
				&& type.HasConstructor()
				&& !type.IsUserDefinedStruct()
				&& !type.IsInterface
				&& Type.GetTypeCode( type ) == TypeCode.Object;

		/// <summary>
		/// Chooses the constructor to use for constuctor mapping.
		/// </summary>
		public virtual Func<Type, ConstructorInfo> GetConstructor { get; set; } = type => type.GetConstructorWithMostParameters();

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
		/// Default is ",";
		/// </summary>
		public virtual string Delimiter
		{
			get { return delimiter; }
			set
			{
				if( value == "\n" )
				{
					throw new ConfigurationException( "Newline is not a valid delimiter." );
				}

				if( value == "\r" )
				{
					throw new ConfigurationException( "Carriage return is not a valid delimiter." );
				}

				if( value == "\0" )
				{
					throw new ConfigurationException( "Null is not a valid delimiter." );
				}

				if( value == Convert.ToString( quote ) )
				{
					throw new ConfigurationException( "You can not use the quote as a delimiter." );
				}

				delimiter = value;

				quoteRequiredChars = BuildRequiredQuoteChars();
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
					throw new ConfigurationException( "Newline is not a valid quote." );
				}

				if( value == '\r' )
				{
					throw new ConfigurationException( "Carriage return is not a valid quote." );
				}

				if( value == '\0' )
				{
					throw new ConfigurationException( "Null is not a valid quote." );
				}

				if( Convert.ToString( value ) == delimiter )
				{
					throw new ConfigurationException( "You can not use the delimiter as a quote." );
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
		public virtual string QuoteString => quoteString;

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		public virtual string DoubleQuoteString => doubleQuoteString;

		/// <summary>
		/// Gets an array characters that require
		/// the field to be quoted.
		/// </summary>
		public virtual char[] QuoteRequiredChars => quoteRequiredChars;

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
		public virtual Encoding Encoding { get; set; } = Encoding.UTF8;

		/// <summary>
		/// Gets or sets the culture info used to read an write CSV files.
		/// </summary>
		public virtual CultureInfo CultureInfo
		{
			get { return cultureInfo; }
			set { cultureInfo = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating if quotes should be
		/// ingored when parsing and treated like any other character.
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
		/// Builds the values for the RequiredQuoteChars property.
		/// </summary>
		public virtual Func<char[]> BuildRequiredQuoteChars { get; set; } 

		/// <summary>
		/// The configured <see cref="ClassMap"/>s.
		/// </summary>
		public virtual ClassMapCollection Maps => maps;

		/// <summary>
		/// Gets or sets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		public virtual bool UseNewObjectForNullReferenceMembers { get; set; } = true;

		/// <summary>
		/// Creates a new CsvConfiguration.
		/// </summary>
		public Configuration()
		{
			maps = new ClassMapCollection( this );

			BuildRequiredQuoteChars = () =>
			{
				return delimiter.Length > 1 ?
					new[] { '\r', '\n' } :
					new[] { '\r', '\n', delimiter[0] };
			};
			quoteRequiredChars = BuildRequiredQuoteChars();
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
			RegisterClassMap( map );

			return map;
		}

		/// <summary>
		/// Use a <see cref="ClassMap{T}" /> to configure mappings.
		/// When using a class map, no members are mapped by default.
		/// Only members specified in the mapping are used.
		/// </summary>
		/// <param name="classMapType">The type of mapping class to use.</param>
		public virtual ClassMap RegisterClassMap( Type classMapType )
		{
			if( !typeof( ClassMap ).IsAssignableFrom( classMapType ) )
			{
				throw new ArgumentException( "The class map type must inherit from CsvClassMap." );
			}

			var map = (ClassMap)ReflectionHelper.CreateInstance( classMapType );
			RegisterClassMap( map );

			return map;
		}

		/// <summary>
		/// Registers the class map.
		/// </summary>
		/// <param name="map">The class map to register.</param>
		public virtual void RegisterClassMap( ClassMap map )
		{
			if( map.MemberMaps.Count == 0 && map.ReferenceMaps.Count == 0 )
			{
				throw new ConfigurationException( "No mappings were specified in the CsvClassMap." );
			}

			Maps.Add( map );
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <typeparam name="TMap">The map type to unregister.</typeparam>
		public virtual void UnregisterClassMap<TMap>() 
			where TMap : ClassMap
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
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <typeparam name="T">The type to generate the map for.</typeparam>
		/// <returns>The generate map.</returns>
		public virtual ClassMap<T> AutoMap<T>()
		{
			var map = ReflectionHelper.CreateInstance<DefaultClassMap<T>>();
			map.AutoMap( this );
			maps.Add( map );

			return map;
		}

		/// <summary>
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <param name="type">The type to generate for the map.</param>
		/// <returns>The generate map.</returns>
		public virtual ClassMap AutoMap( Type type )
		{
			var mapType = typeof( DefaultClassMap<> ).MakeGenericType( type );
			var map = (ClassMap)ReflectionHelper.CreateInstance( mapType );
			map.AutoMap( this );
			maps.Add( map );

			return map;
		}
	}
}
