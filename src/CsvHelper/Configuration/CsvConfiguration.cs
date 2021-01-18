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
		private string delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
		private char escape = '"';
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private string newLine;

		/// <inheritdoc/>
		public virtual bool CacheFields { get; init; }

		/// <inheritdoc/>
		public virtual ParserMode Mode { get; init; }

		/// <inheritdoc/>
		public virtual bool LeaveOpen { get; init; }

		/// <inheritdoc/>
		public virtual bool HasHeaderRecord { get; init; } = true;

		/// <inheritdoc/>
		public virtual HeaderValidated HeaderValidated { get; init; } = ConfigurationFunctions.HeaderValidated;

		/// <inheritdoc/>
		public virtual MissingFieldFound MissingFieldFound { get; init; } = ConfigurationFunctions.MissingFieldFound;

		/// <inheritdoc/>
		public virtual BadDataFound BadDataFound { get; init; } = ConfigurationFunctions.BadDataFound;

		/// <inheritdoc/>
		public virtual ReadingExceptionOccurred ReadingExceptionOccurred { get; init; } = ConfigurationFunctions.ReadingExceptionOccurred;

		/// <inheritdoc/>
		public virtual ShouldSkipRecord ShouldSkipRecord { get; init; } = ConfigurationFunctions.ShouldSkipRecord;

		/// <inheritdoc/>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; init; }

		/// <inheritdoc/>
		public virtual bool SanitizeForInjection { get; init; }

		/// <inheritdoc/>
		public virtual char[] InjectionCharacters { get; init; } = new[] { '=', '@', '+', '-' };

		/// <inheritdoc/>
		public virtual char InjectionEscapeCharacter { get; init; } = '\t';

		/// <inheritdoc/>
		public virtual bool DetectColumnCountChanges { get; init; }

		/// <inheritdoc/>
		public virtual PrepareHeaderForMatch PrepareHeaderForMatch { get; init; } = ConfigurationFunctions.PrepareHeaderForMatch;

		/// <inheritdoc/>
		public virtual ShouldUseConstructorParameters ShouldUseConstructorParameters { get; init; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <inheritdoc/>
		public virtual GetConstructor GetConstructor { get; init; } = ConfigurationFunctions.GetConstructor;

		/// <inheritdoc/>
		public virtual IComparer<string> DynamicPropertySort { get; init; }

		/// <inheritdoc/>
		public virtual GetDynamicPropertyName GetDynamicPropertyName { get; init; } = ConfigurationFunctions.GetDynamicPropertyName;

		/// <inheritdoc/>
		public virtual bool IgnoreReferences { get; init; }

		/// <inheritdoc/>
		public virtual TrimOptions TrimOptions { get; init; }

		/// <inheritdoc/>
		public virtual char[] WhiteSpaceChars { get; init; } = new char[] { ' ', '\t' };

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
		public virtual string DoubleQuoteString => doubleQuoteString;

		/// <inheritdoc/>
		public ShouldQuote ShouldQuote { get; init; } = ConfigurationFunctions.ShouldQuote;

		/// <inheritdoc/>
		public virtual char Comment { get; init; } = '#';

		/// <inheritdoc/>
		public virtual bool AllowComments { get; init; }

		/// <inheritdoc/>
		public virtual int BufferSize { get; init; } = 0x1000;

		/// <inheritdoc/>
		public virtual bool CountBytes { get; init; }

		/// <inheritdoc/>
		public virtual Encoding Encoding { get; init; } = Encoding.UTF8;

		/// <inheritdoc/>
		public virtual CultureInfo CultureInfo { get; protected set; }

		/// <inheritdoc/>
		public virtual bool IncludePrivateMembers { get; init; }

		/// <inheritdoc/>
		public virtual MemberTypes MemberTypes { get; init; } = MemberTypes.Properties;

		/// <inheritdoc/>
		public virtual bool IgnoreBlankLines { get; init; } = true;

		/// <inheritdoc/>
		public virtual ReferenceHeaderPrefix ReferenceHeaderPrefix { get; init; }

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
		public bool IsNewLineSet { get; private set; }

		/// <inheritdoc/>
		public virtual bool UseNewObjectForNullReferenceMembers { get; init; } = true;

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
			newLine = "\r\n";
		}
	}
}
