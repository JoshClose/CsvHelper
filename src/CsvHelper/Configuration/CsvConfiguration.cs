// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public record CsvConfiguration : IReaderConfiguration, IWriterConfiguration
	{
		private string delimiter;
		private char escape = '"';
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private string newLine = "\r\n";

		/// <inheritdoc/>
		public virtual bool AllowComments { get; init; }

		/// <inheritdoc/>
		public virtual BadDataFound BadDataFound { get; init; } = ConfigurationFunctions.BadDataFound;

		/// <inheritdoc/>
		public virtual int BufferSize { get; init; } = 0x1000;

		/// <inheritdoc/>
		public virtual bool CacheFields { get; init; }

		/// <inheritdoc/>
		public virtual char Comment { get; init; } = '#';

		/// <inheritdoc/>
		public virtual bool CountBytes { get; init; }

		/// <inheritdoc/>
		public virtual CultureInfo CultureInfo { get; protected set; }

		/// <inheritdoc/>
		public virtual string Delimiter
		{
			get { return delimiter; }
			init
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

		/// <inheritdoc/>
		public virtual bool DetectColumnCountChanges { get; init; }

		/// <inheritdoc/>
		public virtual string DoubleQuoteString => doubleQuoteString;

		/// <inheritdoc/>
		public virtual IComparer<string> DynamicPropertySort { get; init; }

		/// <inheritdoc/>
		public virtual Encoding Encoding { get; init; } = Encoding.UTF8;

		/// <inheritdoc/>
		public virtual char Escape
		{
			get { return escape; }
			init
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

		/// <inheritdoc/>
		public virtual GetConstructor GetConstructor { get; init; } = ConfigurationFunctions.GetConstructor;

		/// <inheritdoc/>
		public virtual GetDynamicPropertyName GetDynamicPropertyName { get; init; } = ConfigurationFunctions.GetDynamicPropertyName;

		/// <inheritdoc/>
		public virtual bool HasHeaderRecord { get; init; } = true;

		/// <inheritdoc/>
		public virtual HeaderValidated HeaderValidated { get; init; } = ConfigurationFunctions.HeaderValidated;

		/// <inheritdoc/>
		public virtual bool IgnoreBlankLines { get; init; } = true;

		/// <inheritdoc/>
		public virtual bool IgnoreReferences { get; init; }

		/// <inheritdoc/>
		public virtual bool IncludePrivateMembers { get; init; }

		/// <inheritdoc/>
		public virtual char[] InjectionCharacters { get; init; } = new[] { '=', '@', '+', '-' };

		/// <inheritdoc/>
		public virtual char InjectionEscapeCharacter { get; init; } = '\t';

		/// <inheritdoc/>
		public bool IsNewLineSet { get; private set; }

		/// <inheritdoc/>
		public virtual bool LeaveOpen { get; init; }

		/// <inheritdoc/>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; init; }

		/// <inheritdoc/>
		public virtual MemberTypes MemberTypes { get; init; } = MemberTypes.Properties;

		/// <inheritdoc/>
		public virtual MissingFieldFound MissingFieldFound { get; init; } = ConfigurationFunctions.MissingFieldFound;

		/// <inheritdoc/>
		public virtual ParserMode Mode { get; init; }

		/// <inheritdoc/>
		public virtual string NewLine
		{
			get => newLine;
			init
			{
				IsNewLineSet = true;
				newLine = value;
			}
		}

		/// <inheritdoc/>
		public virtual PrepareHeaderForMatch PrepareHeaderForMatch { get; init; } = ConfigurationFunctions.PrepareHeaderForMatch;

		/// <inheritdoc/>
		public virtual int ProcessFieldBufferSize { get; init; } = 1024;

		/// <inheritdoc/>
		public virtual char Quote
		{
			get { return quote; }
			init
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

				quoteString = Convert.ToString(value, CultureInfo);
				doubleQuoteString = escape + quoteString;
			}
		}

		/// <inheritdoc/>
		public virtual string QuoteString => quoteString;

		/// <inheritdoc/>
		public virtual ReadingExceptionOccurred ReadingExceptionOccurred { get; init; } = ConfigurationFunctions.ReadingExceptionOccurred;

		/// <inheritdoc/>
		public virtual ReferenceHeaderPrefix ReferenceHeaderPrefix { get; init; }

		/// <inheritdoc/>
		public virtual bool SanitizeForInjection { get; init; }

		/// <inheritdoc/>
		public ShouldQuote ShouldQuote { get; init; } = ConfigurationFunctions.ShouldQuote;

		/// <inheritdoc/>
		public virtual ShouldSkipRecord ShouldSkipRecord { get; init; } = ConfigurationFunctions.ShouldSkipRecord;

		/// <inheritdoc/>
		public virtual ShouldUseConstructorParameters ShouldUseConstructorParameters { get; init; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <inheritdoc/>
		public virtual TrimOptions TrimOptions { get; init; }

		/// <inheritdoc/>
		public virtual bool UseNewObjectForNullReferenceMembers { get; init; } = true;

		/// <inheritdoc/>
		public virtual char[] WhiteSpaceChars { get; init; } = new char[] { ' ', '\t' };

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvConfiguration"/> class
		/// using the given <see cref="System.Globalization.CultureInfo"/>. Since <see cref="Delimiter"/>
		/// uses <see cref="CultureInfo"/> for it's default, the given <see cref="System.Globalization.CultureInfo"/>
		/// will be used instead.
		/// </summary>
		/// <param name="cultureInfo">The culture information.</param>
		public CsvConfiguration(CultureInfo cultureInfo)
		{
			CultureInfo = cultureInfo;
			delimiter = cultureInfo.TextInfo.ListSeparator;
		}

		/// <summary>
		/// Use property initializers instead of this constructor if your environment allows it.
		/// Initializes a new instance of the <see cref="CsvConfiguration"/> class.
		/// Any arguments passed in as null will use their default value instead.
		/// If you need to set a callback like <see cref="MissingFieldFound"/> to null, set it to an empty
		/// function instead.
		/// <code>
		///		missingFieldFound: () => { }
		/// </code>
		/// </summary>
		/// <param name="cultureInfo">The the culture info used to read and write CSV files.</param>
		/// <param name="allowComments">A value indicating if comments are allowed. <c>true</c> to allow commented out lines, otherwise <c>false</c>.</param>
		/// <param name="badDataFound">The function that is called when bad field data is found. A field has bad data if it contains a quote and the field is not quoted (escaped). You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: context</param>
		/// <param name="bufferSize">Size of the buffer used for parsing and writing CSV files. Default is 0x1000.</param>
		/// <param name="cacheFields">Cache fields that are created when parsing. Default is false.</param>
		/// <param name="comment">The character used to denote a line that is commented out. Default is '#'.</param>
		/// <param name="countBytes">A value indicating whether the number of bytes should be counted while parsing. Default is false. This will slow down parsing because it needs to get the byte count of every char for the given encoding. The <see cref="Encoding"/> needs to be set correctly for this to be accurate.</param>
		/// <param name="delimiter">The delimiter used to separate fields. Default is <see cref="TextInfo.ListSeparator"/>.</param>
		/// <param name="detectColumnCountChanges">A value indicating whether changes in the column count should be detected. If true, a <see cref="BadDataException"/> will be thrown if a different column count is detected.</param>
		/// <param name="dynamicPropertySort">The comparer used to order the properties of dynamic objects when writing. The default is null, which will preserve the order the object properties were created with.</param>
		/// <param name="encoding">The encoding used when counting bytes.</param>
		/// <param name="escape">The character used to escape characters. Default is '"'.</param>
		/// <param name="getConstructor">Chooses the constructor to use for constructor mapping. Arguments: (classType)</param>
		/// <param name="getDynamicPropertyName">The name to use for the property of the dynamic object. Arguments: (readingContext, fieldIndex)</param>
		/// <param name="hasHeaderRecord">A value indicating if the CSV file has a header record. Default is true.</param>
		/// <param name="headerValidated">The function that is called when a header validation check is ran. The default function will throw a <see cref="ValidationException"/> if there is no header for a given member mapping. You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: (isValid, headerNames, headerNameIndex, context)</param>
		/// <param name="ignoreBlankLines">A value indicating if blank lines should be ignored when reading. <c>true</c> to ignore, otherwise <c>false</c>. Default is true.</param>
		/// <param name="ignoreReferences">A value indicating whether references should be ignored when auto mapping. <c>true</c> to ignore references, otherwise <c>false</c>. Default is false.</param>
		/// <param name="includePrivateMembers">A value indicating if private member should be read from and written to. <c>true</c> to include private member, otherwise <c>false</c>. Default is false.</param>
		/// <param name="injectionCharacters">The characters that are used for injection attacks.</param>
		/// <param name="injectionEscapeCharacter">The character used to escape a detected injection.</param>
		/// <param name="leaveOpen">A value indicating whether to leave the <see cref="TextReader"/> or <see cref="TextWriter"/> open after this object is disposed.</param>
		/// <param name="lineBreakInQuotedFieldIsBadData">A value indicating if a line break found in a quote field should be considered bad data. <c>true</c> to consider a line break bad data, otherwise <c>false</c>. Defaults to false.</param>
		/// <param name="memberTypes">The member types that are used when auto mapping. MemberTypes are flags, so you can choose more than one. Default is Properties.</param>
		/// <param name="missingFieldFound">The function that is called when a missing field is found. The default function will throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: (headerNames, index, context)</param>
		/// <param name="mode">The parsing mode.</param>
		/// <param name="newLine">The newline string to use. Default is <see cref="Environment.NewLine"/>. When writing, this value is always used. When reading, this value is only used if explicitly set. If not set, the parser uses one of \r\n, \r, or \n.</param>
		/// <param name="prepareHeaderForMatch">Prepares the header field for matching against a member name. The header field and the member name are both ran through this function. You should do things like trimming, removing whitespace, removing underscores, and making casing changes to ignore case. Arguments: (header, fieldIndex)</param>
		/// <param name="processFieldBufferSize">Size of the buffer to process a field. Should be larger than the largest field.</param>
		/// <param name="quote">The character used to quote fields. Default is '"'.</param>
		/// <param name="readingExceptionOccurred">The function that is called when a reading exception occurs. The default function will re-throw the given exception. If you want to ignore reading exceptions, you can supply your own function to do other things like logging the issue. Arguments: (exception)</param>
		/// <param name="referenceHeaderPrefix">A callback that will return the prefix for a reference header. Arguments: (memberType, memberName)</param>
		/// <param name="sanitizeForInjection">A value indicating if fields should be sanitized to prevent malicious injection. This covers MS Excel,  Google Sheets and Open Office Calc.</param>
		/// <param name="shouldQuote">A function that is used to determine if a field should get quoted  when writing. Arguments: field, context</param>
		/// <param name="shouldSkipRecord">The callback that will be called to determine whether to skip the given record or not. Arguments: (record)</param>
		/// <param name="shouldUseConstructorParameters">Determines if constructor parameters should be used to create the class instead of the default constructor and members. Arguments: (parameterType)</param>
		/// <param name="trimOptions">The field trimming options.</param>
		/// <param name="useNewObjectForNullReferenceMembers">A value indicating that during writing if a new  object should be created when a reference member is null. True to create a new object and use it's defaults for the fields, or false to leave the fields empty for all the reference member's member.</param>
		/// <param name="whiteSpaceChars">Characters considered whitespace. Used when trimming fields.</param>
		public CsvConfiguration(
			CultureInfo cultureInfo,
			bool? allowComments = null,
			BadDataFound badDataFound = null,
			int? bufferSize = null,
			bool? cacheFields = null,
			char? comment = null,
			bool? countBytes = null,
			string delimiter = null,
			bool? detectColumnCountChanges = null,
			IComparer<string> dynamicPropertySort = null,
			Encoding encoding = null,
			char? escape = null,
			GetConstructor getConstructor = null,
			GetDynamicPropertyName getDynamicPropertyName = null,
			bool? hasHeaderRecord = null,
			HeaderValidated headerValidated = null,
			bool? ignoreBlankLines = null,
			bool? ignoreReferences = null,
			bool? includePrivateMembers = null,
			char[] injectionCharacters = null,
			char? injectionEscapeCharacter = null,
			bool? leaveOpen = null,
			bool? lineBreakInQuotedFieldIsBadData = null,
			MemberTypes? memberTypes = null,
			MissingFieldFound missingFieldFound = null,
			ParserMode? mode = null,
			string newLine = null,
			PrepareHeaderForMatch prepareHeaderForMatch = null,
			int? processFieldBufferSize = null,
			char? quote = null,
			ReadingExceptionOccurred readingExceptionOccurred = null,
			ReferenceHeaderPrefix referenceHeaderPrefix = null,
			bool? sanitizeForInjection = null,
			ShouldQuote shouldQuote = null,
			ShouldSkipRecord shouldSkipRecord = null,
			ShouldUseConstructorParameters shouldUseConstructorParameters = null,
			TrimOptions? trimOptions = null,
			bool? useNewObjectForNullReferenceMembers = null,
			char[] whiteSpaceChars = null
		)
		{
			CultureInfo = cultureInfo;
			AllowComments = allowComments ?? AllowComments;
			BadDataFound = badDataFound ?? BadDataFound;
			BufferSize = bufferSize ?? BufferSize;
			CacheFields = cacheFields ?? CacheFields;
			Comment = comment ?? Comment;
			CountBytes = countBytes ?? CountBytes;
			Delimiter = delimiter ?? cultureInfo.TextInfo.ListSeparator;
			DetectColumnCountChanges = detectColumnCountChanges ?? DetectColumnCountChanges;
			DynamicPropertySort = dynamicPropertySort ?? DynamicPropertySort;
			Encoding = encoding ?? Encoding;
			Escape = escape ?? Escape;
			GetConstructor = getConstructor ?? GetConstructor;
			GetDynamicPropertyName = getDynamicPropertyName ?? GetDynamicPropertyName;
			HasHeaderRecord = hasHeaderRecord ?? HasHeaderRecord;
			HeaderValidated = headerValidated ?? HeaderValidated;
			IgnoreBlankLines = ignoreBlankLines ?? IgnoreBlankLines;
			IgnoreReferences = ignoreReferences ?? IgnoreReferences;
			IncludePrivateMembers = includePrivateMembers ?? IncludePrivateMembers;
			InjectionCharacters = injectionCharacters ?? InjectionCharacters;
			InjectionEscapeCharacter = injectionEscapeCharacter ?? InjectionEscapeCharacter;
			LeaveOpen = leaveOpen ?? LeaveOpen;
			LineBreakInQuotedFieldIsBadData = lineBreakInQuotedFieldIsBadData ?? LineBreakInQuotedFieldIsBadData;
			MemberTypes = memberTypes ?? MemberTypes;
			MissingFieldFound = missingFieldFound ?? MissingFieldFound;
			Mode = mode ?? Mode;
			NewLine = newLine ?? NewLine;
			PrepareHeaderForMatch = prepareHeaderForMatch ?? PrepareHeaderForMatch;
			ProcessFieldBufferSize = processFieldBufferSize ?? ProcessFieldBufferSize;
			Quote = quote ?? Quote;
			ReadingExceptionOccurred = readingExceptionOccurred ?? ReadingExceptionOccurred;
			ReferenceHeaderPrefix = referenceHeaderPrefix ?? ReferenceHeaderPrefix;
			SanitizeForInjection = sanitizeForInjection ?? SanitizeForInjection;
			ShouldQuote = shouldQuote ?? ShouldQuote;
			ShouldSkipRecord = shouldSkipRecord ?? ShouldSkipRecord;
			ShouldUseConstructorParameters = shouldUseConstructorParameters ?? ShouldUseConstructorParameters;
			TrimOptions = trimOptions ?? TrimOptions;
			UseNewObjectForNullReferenceMembers = useNewObjectForNullReferenceMembers ?? UseNewObjectForNullReferenceMembers;
			WhiteSpaceChars = whiteSpaceChars ?? WhiteSpaceChars;
		}
	}
}
