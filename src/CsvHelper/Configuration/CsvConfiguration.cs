// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Delegates;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public record CsvConfiguration : IReaderConfiguration, IWriterConfiguration
	{
		private string newLine = "\r\n";

		/// <inheritdoc/>
		public virtual bool AllowComments { get; set; }

		/// <inheritdoc/>
		public virtual BadDataFound BadDataFound { get; set; } = ConfigurationFunctions.BadDataFound;

		/// <inheritdoc/>
		public virtual int BufferSize { get; set; } = 0x1000;

		/// <inheritdoc/>
		public virtual bool CacheFields { get; set; }

		/// <inheritdoc/>
		public virtual char Comment { get; set; } = '#';

		/// <inheritdoc/>
		public virtual bool CountBytes { get; set; }

		/// <inheritdoc/>
		public virtual CultureInfo CultureInfo { get; protected set; }

		/// <inheritdoc/>
		public virtual string Delimiter { get; set; }

		/// <inheritdoc/>
		public virtual bool DetectDelimiter { get; set; }

		/// <inheritdoc/>
		public virtual GetDelimiter GetDelimiter { get; set; } = ConfigurationFunctions.GetDelimiter;

		/// <inheritdoc/>
		public virtual string[] DetectDelimiterValues { get; set; } = new[] { ",", ";", "|", "\t" };

		/// <inheritdoc/>
		public virtual bool DetectColumnCountChanges { get; set; }

		/// <inheritdoc/>
		public virtual IComparer<string>? DynamicPropertySort { get; set; }

		/// <inheritdoc/>
		public virtual Encoding Encoding { get; set; } = Encoding.UTF8;

		/// <inheritdoc/>
		public virtual char Escape { get; set; } = '"';

		/// <inheritdoc/>
		public virtual bool ExceptionMessagesContainRawData { get; set; } = true;

		/// <inheritdoc/>
		public virtual GetConstructor GetConstructor { get; set; } = ConfigurationFunctions.GetConstructor;

		/// <inheritdoc/>
		public virtual GetDynamicPropertyName GetDynamicPropertyName { get; set; } = ConfigurationFunctions.GetDynamicPropertyName;

		/// <inheritdoc/>
		public virtual bool HasHeaderRecord { get; set; } = true;

		/// <inheritdoc/>
		public virtual HeaderValidated HeaderValidated { get; set; } = ConfigurationFunctions.HeaderValidated;

		/// <inheritdoc/>
		public virtual bool IgnoreBlankLines { get; set; } = true;

		/// <inheritdoc/>
		public virtual bool IgnoreReferences { get; set; }

		/// <inheritdoc/>
		public virtual bool IncludePrivateMembers { get; set; }

		/// <inheritdoc/>
		public virtual char[] InjectionCharacters { get; set; } = new[] { '=', '@', '+', '-', '\t', '\r' };

		/// <inheritdoc/>
		public virtual char InjectionEscapeCharacter { get; set; } = '\'';

		/// <inheritdoc />
		public virtual InjectionOptions InjectionOptions { get; set; }

		/// <inheritdoc/>
		public bool IsNewLineSet { get; private set; }

		/// <inheritdoc/>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; set; }

		/// <inheritdoc/>
		public double MaxFieldSize { get; set; }

		/// <inheritdoc/>
		public virtual MemberTypes MemberTypes { get; set; } = MemberTypes.Properties;

		/// <inheritdoc/>
		public virtual MissingFieldFound MissingFieldFound { get; set; } = ConfigurationFunctions.MissingFieldFound;

		/// <inheritdoc/>
		public virtual CsvMode Mode { get; set; }

		/// <inheritdoc/>
		public virtual string NewLine
		{
			get => newLine;
			set
			{
				IsNewLineSet = true;
				newLine = value;
			}
		}

		/// <inheritdoc/>
		public virtual PrepareHeaderForMatch PrepareHeaderForMatch { get; set; } = ConfigurationFunctions.PrepareHeaderForMatch;

		/// <inheritdoc/>
		public virtual int ProcessFieldBufferSize { get; set; } = 1024;

		/// <inheritdoc/>
		public virtual char Quote { get; set; } = '"';

		/// <inheritdoc/>
		public virtual ReadingExceptionOccurred ReadingExceptionOccurred { get; set; } = ConfigurationFunctions.ReadingExceptionOccurred;

		/// <inheritdoc/>
		public virtual ReferenceHeaderPrefix? ReferenceHeaderPrefix { get; set; }

		/// <inheritdoc/>
		public ShouldQuote ShouldQuote { get; set; } = ConfigurationFunctions.ShouldQuote;

		/// <inheritdoc/>
		public virtual ShouldSkipRecord? ShouldSkipRecord { get; set; }

		/// <inheritdoc/>
		public virtual ShouldUseConstructorParameters ShouldUseConstructorParameters { get; set; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <inheritdoc/>
		public virtual TrimOptions TrimOptions { get; set; }

		/// <inheritdoc/>
		public virtual bool UseNewObjectForNullReferenceMembers { get; set; } = true;

		/// <inheritdoc/>
		public virtual char[] WhiteSpaceChars { get; set; } = new char[] { ' ' };

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvConfiguration"/> class
		/// using the given <see cref="System.Globalization.CultureInfo"/>. Since <see cref="Delimiter"/>
		/// uses <see cref="CultureInfo"/> for it's default, the given <see cref="System.Globalization.CultureInfo"/>
		/// will be used instead.
		/// </summary>
		/// <param name="cultureInfo">The culture information.</param>
		/// <param name="attributesType">The type that contains the configuration attributes.
		/// This will call <see cref="ApplyAttributes(Type)"/> automatically.</param>
		public CsvConfiguration(CultureInfo cultureInfo, Type? attributesType = null)
		{
			CultureInfo = cultureInfo;
			Delimiter = cultureInfo.TextInfo.ListSeparator;

			if (attributesType != null)
			{
				ApplyAttributes(attributesType);
			}
		}

		/// <summary>
		/// Validates the configuration.
		/// </summary>
		public void Validate()
		{
			var escape = Escape.ToString();
			var quote = Quote.ToString();
			var lineEndings = new[] { "\r", "\n", "\r\n" };
			var whiteSpaceChars = WhiteSpaceChars.Select(c => c.ToString()).ToArray();

			// Escape
			if (escape == Delimiter) throw new ConfigurationException($"The escape character '{Escape}' and delimiter '{Delimiter}' cannot be the same.");
			if (escape == NewLine && IsNewLineSet) throw new ConfigurationException($"The escape character '{Escape}' and new line '{NewLine}' cannot be the same.");
			if (lineEndings.Contains(Escape.ToString()) && !IsNewLineSet) throw new ConfigurationException($"The escape character '{Escape}' cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(escape)) throw new ConfigurationException($"The escape character '{Escape}' cannot be a WhiteSpaceChar.");

			// Quote
			if (quote == Delimiter) throw new ConfigurationException($"The quote character '{Quote}' and the delimiter '{Delimiter}' cannot be the same.");
			if (quote == NewLine && IsNewLineSet) throw new ConfigurationException($"The quote character '{Quote}' and new line '{NewLine}' cannot be the same.");
			if (lineEndings.Contains(quote)) throw new ConfigurationException($"The quote character '{Quote}' cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(quote)) throw new ConfigurationException($"The quote character '{Quote}' cannot be a WhiteSpaceChar.");

			// Delimiter
			if (Delimiter == NewLine && IsNewLineSet) throw new ConfigurationException($"The delimiter '{Delimiter}' and new line '{NewLine}' cannot be the same.");
			if (lineEndings.Contains(Delimiter)) throw new ConfigurationException($"The delimiter '{Delimiter}' cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(Delimiter)) throw new ConfigurationException($"The delimiter '{Delimiter}' cannot be a WhiteSpaceChar.");

			// Detect Delimiter
			if (DetectDelimiter && DetectDelimiterValues.Length == 0) throw new ConfigurationException($"At least one value is required for {nameof(DetectDelimiterValues)} when {nameof(DetectDelimiter)} is enabled.");
		}

		/// <summary>
		/// Applies class level attribute to configuration.
		/// </summary>
		/// <typeparam name="T">Type with attributes.</typeparam>
		public CsvConfiguration ApplyAttributes<T>()
		{
			return ApplyAttributes(typeof(T));
		}

		/// <summary>
		/// Applies class level attribute to configuration.
		/// </summary>
		/// <param name="type">Type with attributes.</param>
		public CsvConfiguration ApplyAttributes(Type type)
		{
			var attributes = type.GetCustomAttributes().OfType<IClassMapper>();
			foreach (var attribute in attributes)
			{
				attribute.ApplyTo(this);
			}

			return this;
		}
	}
}
