// Copyright 2009-2021 Josh Close
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
		public virtual string[] DelimiterValues { get; set; } = new[] { ",", ";", "|", "\t" };

		/// <inheritdoc/>
		public virtual bool DetectColumnCountChanges { get; set; }

		/// <inheritdoc/>
		public virtual IComparer<string> DynamicPropertySort { get; set; }

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
		public virtual char[] InjectionCharacters { get; set; } = new[] { '=', '@', '+', '-' };

		/// <inheritdoc/>
		public virtual char InjectionEscapeCharacter { get; set; } = '\t';

		/// <inheritdoc/>
		public bool IsNewLineSet { get; private set; }

		/// <inheritdoc/>
		public virtual bool LeaveOpen { get; set; }

		/// <inheritdoc/>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; set; }

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
		public virtual ReferenceHeaderPrefix ReferenceHeaderPrefix { get; set; }

		/// <inheritdoc/>
		public virtual bool SanitizeForInjection { get; set; }

		/// <inheritdoc/>
		public ShouldQuote ShouldQuote { get; set; } = ConfigurationFunctions.ShouldQuote;

		/// <inheritdoc/>
		public virtual ShouldSkipRecord ShouldSkipRecord { get; set; } = ConfigurationFunctions.ShouldSkipRecord;

		/// <inheritdoc/>
		public virtual ShouldUseConstructorParameters ShouldUseConstructorParameters { get; set; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <inheritdoc/>
		public virtual TrimOptions TrimOptions { get; set; }

		/// <inheritdoc/>
		public virtual bool UseNewObjectForNullReferenceMembers { get; set; } = true;

		/// <inheritdoc/>
		public virtual char[] WhiteSpaceChars { get; set; } = new char[] { ' ', '\t' };

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
			Delimiter = cultureInfo.TextInfo.ListSeparator;
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
			if (escape == Delimiter) throw new ConfigurationException($"{Escape} and {Delimiter} cannot be the same.");
			if (escape == NewLine && IsNewLineSet) throw new ConfigurationException($"{Escape} and {NewLine} cannot be the same.");
			if (lineEndings.Contains(Escape.ToString()) && !IsNewLineSet) throw new ConfigurationException($"{Escape} cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(escape)) throw new ConfigurationException($"{Escape} cannot be a WhiteSpaceChar.");

			// Quote
			if (quote == Delimiter) throw new ConfigurationException($"{Quote} and {Delimiter} cannot be the same.");
			if (quote == NewLine && IsNewLineSet) throw new ConfigurationException($"{Quote} and {NewLine} cannot be the same.");
			if (lineEndings.Contains(quote)) throw new ConfigurationException($"{Quote} cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(quote)) throw new ConfigurationException($"{Quote} cannot be a WhiteSpaceChar.");

			// Delimiter
			if (Delimiter == NewLine && IsNewLineSet) throw new ConfigurationException($"{Delimiter} and {NewLine} cannot be the same.");
			if (lineEndings.Contains(Delimiter)) throw new ConfigurationException($"{Delimiter} cannot be a line ending. ('\\r', '\\n', '\\r\\n')");
			if (whiteSpaceChars.Contains(Delimiter)) throw new ConfigurationException($"{Delimiter} cannot be a WhiteSpaceChar.");

			// Detect Delimiter
			if (DetectDelimiter && DelimiterValues.Length == 0) throw new ConfigurationException($"At least one value is required for {nameof(DelimiterValues)} when {nameof(DetectDelimiter)} is enabled.");
		}
	}
}
