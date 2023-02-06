// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvHelper.Configuration
{
	/// <summary>Holds the default callback methods for delegate members of <c>CsvHelper.Configuration.Configuration</c>.</summary>
	public static class ConfigurationFunctions
	{
		private static readonly char[] lineEndingChars = new char[] { '\r', '\n' };

		/// <summary>
		/// Throws a <see cref="ValidationException"/> if <see name="HeaderValidatedArgs.InvalidHeaders"/> is not empty.
		/// </summary>
		public static void HeaderValidated(HeaderValidatedArgs args)
		{
			if (args.InvalidHeaders.Count() == 0)
			{
				return;
			}

			var errorMessage = new StringBuilder();
			foreach (var invalidHeader in args.InvalidHeaders)
			{
				errorMessage.AppendLine($"Header with name '{string.Join("' or '", invalidHeader.Names)}'[{invalidHeader.Index}] was not found.");
			}

			if (args.Context.Reader.HeaderRecord != null)
			{
				foreach (var header in args.Context.Reader.HeaderRecord)
				{
					errorMessage.AppendLine($"Headers: '{string.Join("', '", args.Context.Reader.HeaderRecord)}'");
				}
			}

			var messagePostfix =
				$"If you are expecting some headers to be missing and want to ignore this validation, " +
				$"set the configuration {nameof(HeaderValidated)} to null. You can also change the " +
				$"functionality to do something else, like logging the issue.";
			errorMessage.AppendLine(messagePostfix);

			throw new HeaderValidationException(args.Context, args.InvalidHeaders, errorMessage.ToString());
		}

		/// <summary>
		/// Throws a <c>MissingFieldException</c>.
		/// </summary>
		public static void MissingFieldFound(MissingFieldFoundArgs args)
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof(MissingFieldFound)} to null.";

			// Get by index.

			if (args.HeaderNames == null || args.HeaderNames.Length == 0)
			{
				throw new MissingFieldException(args.Context, $"Field at index '{args.Index}' does not exist. {messagePostfix}");
			}

			// Get by name.

			var indexText = args.Index > 0 ? $" at field index '{args.Index}'" : string.Empty;

			if (args.HeaderNames.Length == 1)
			{
				throw new MissingFieldException(args.Context, $"Field with name '{args.HeaderNames[0]}'{indexText} does not exist. {messagePostfix}");
			}

			throw new MissingFieldException(args.Context, $"Field containing names '{string.Join("' or '", args.HeaderNames)}'{indexText} does not exist. {messagePostfix}");
		}

		/// <summary>
		/// Throws a <see cref="BadDataException"/>.
		/// </summary>
		public static void BadDataFound(BadDataFoundArgs args)
		{
			throw new BadDataException(args.Field, args.RawRecord, args.Context, $"You can ignore bad data by setting {nameof(BadDataFound)} to null.");
		}

		/// <summary>
		/// Throws the given <see name="ReadingExceptionOccurredArgs.Exception"/>.
		/// </summary>
		public static bool ReadingExceptionOccurred(ReadingExceptionOccurredArgs args)
		{
			return true;
		}

		/// <summary>
		/// Returns true if the field contains a <see cref="IWriterConfiguration.Quote"/>,
		/// starts with a space, ends with a space, contains \r or \n, or contains
		/// the <see cref="IWriterConfiguration.Delimiter"/>.
		/// </summary>
		/// <param name="args">The args.</param>
		/// <returns><c>true</c> if the field should be quoted, otherwise <c>false</c>.</returns>
		public static bool ShouldQuote(ShouldQuoteArgs args)
		{
			var config = args.Row.Configuration;

			var shouldQuote = !string.IsNullOrEmpty(args.Field) &&
			(
				args.Field.Contains(config.Quote) // Contains quote
				|| args.Field[0] == ' ' // Starts with a space
				|| args.Field[args.Field.Length - 1] == ' ' // Ends with a space
				|| (config.Delimiter.Length > 0 && args.Field.Contains(config.Delimiter)) // Contains delimiter
				|| !config.IsNewLineSet && args.Field.IndexOfAny(lineEndingChars) > -1 // Contains line ending characters
				|| config.IsNewLineSet && args.Field.Contains(config.NewLine) // Contains newline
			);

			return shouldQuote;
		}

		/// <summary>
		/// Returns the <see name="PrepareHeaderForMatchArgs.Header"/> as given.
		/// </summary>
		public static string PrepareHeaderForMatch(PrepareHeaderForMatchArgs args)
		{
			return args.Header;
		}

		/// <summary>
		/// Returns <c>true</c> if <paramref name="args.ParameterType"/>:
		/// 1. does not have a parameterless constructor
		/// 2. has a constructor
		/// 3. is not a value type
		/// 4. is not a primitive
		/// 5. is not an enum
		/// 6. is not an interface
		/// 7. TypeCode is an Object.
		/// </summary>
		public static bool ShouldUseConstructorParameters(ShouldUseConstructorParametersArgs args)
		{
			return !args.ParameterType.HasParameterlessConstructor()
				&& args.ParameterType.HasConstructor()
				&& !args.ParameterType.IsValueType
				&& !args.ParameterType.IsPrimitive
				&& !args.ParameterType.IsEnum
				&& !args.ParameterType.IsInterface
				&& Type.GetTypeCode(args.ParameterType) == TypeCode.Object;
		}

		/// <summary>
		/// Returns the type's constructor with the most parameters. 
		/// If two constructors have the same number of parameters, then
		/// there is no guarantee which one will be returned. If you have
		/// that situation, you should probably implement this function yourself.
		/// </summary>
		public static ConstructorInfo GetConstructor(GetConstructorArgs args)
		{
			return args.ClassType.GetConstructorWithMostParameters();
		}

		/// <summary>
		/// Returns the header name ran through <see cref="PrepareHeaderForMatch(PrepareHeaderForMatchArgs)"/>.
		/// If no header exists, property names will be Field1, Field2, Field3, etc.
		/// </summary>
		/// <param name="args">The args.</param>
		public static string GetDynamicPropertyName(GetDynamicPropertyNameArgs args)
		{
			if (args.Context.Reader.HeaderRecord == null)
			{
				return $"Field{args.FieldIndex + 1}";
			}

			var header = args.Context.Reader.HeaderRecord[args.FieldIndex];
			var prepareHeaderForMatchArgs = new PrepareHeaderForMatchArgs(header, args.FieldIndex);
			header = args.Context.Reader.Configuration.PrepareHeaderForMatch(prepareHeaderForMatchArgs);

			return header;
		}

		/// <summary>
		/// Detects the delimiter based on the given text.
		/// Return the detected delimiter or null if one wasn't found.
		/// </summary>
		/// <param name="args">The args.</param>
		public static string? GetDelimiter(GetDelimiterArgs args)
		{
			var text = args.Text;
			var config = args.Configuration;

			if (config.Mode == CsvMode.RFC4180)
			{
				// Remove text in between pairs of quotes.
				text = Regex.Replace(text, $"{config.Quote}.*?{config.Quote}", string.Empty, RegexOptions.Singleline);
			}
			else if (config.Mode == CsvMode.Escape)
			{
				// Remove escaped characters.
				text = Regex.Replace(text, $"({config.Escape}.)", string.Empty, RegexOptions.Singleline);
			}

			var newLine = config.NewLine;
			if ((new[] { "\r\n", "\r", "\n" }).Contains(newLine))
			{
				newLine = "\r\n|\r|\n";
			}

			var lineDelimiterCounts = new List<Dictionary<string, int>>();
			while (text.Length > 0)
			{
				// Since all escaped text has been removed, we can reliably read line by line.
				var match = Regex.Match(text, newLine);
				var line = match.Success ? text.Substring(0, match.Index + match.Length) : text;

				var delimiterCounts = new Dictionary<string, int>();
				foreach (var delimiter in config.DetectDelimiterValues)
				{
					// Escape regex special chars to use as regex pattern.
					var pattern = Regex.Replace(delimiter, @"([.$^{\[(|)*+?\\])", "\\$1");
					delimiterCounts[delimiter] = Regex.Matches(line, pattern).Count;
				}

				lineDelimiterCounts.Add(delimiterCounts);

				text = match.Success ? text.Substring(match.Index + match.Length) : string.Empty;
			}

			if (lineDelimiterCounts.Count > 1)
			{
				// The last line isn't complete and can't be used to reliably detect a delimiter.
				lineDelimiterCounts.Remove(lineDelimiterCounts.Last());
			}

			// Rank only the delimiters that appear on every line.
			var delimiters =
			(
				from counts in lineDelimiterCounts
				from count in counts
				group count by count.Key into g
				where g.All(x => x.Value > 0)
				let sum = g.Sum(x => x.Value)
				orderby sum descending
				select new
				{
					Delimiter = g.Key,
					Count = sum
				}
			).ToList();

			string? newDelimiter = null;
			if (!config.IgnoreCultureListSeparator && delimiters.Any(x => x.Delimiter == config.CultureInfo.TextInfo.ListSeparator) && lineDelimiterCounts.Count > 1)
			{
				// The culture's separator is on every line. Assume this is the delimiter.
				newDelimiter = config.CultureInfo.TextInfo.ListSeparator;
			}
			else
			{
				// Choose the highest ranked delimiter.
				newDelimiter = delimiters.Select(x => x.Delimiter).FirstOrDefault();
			}

			if (newDelimiter != null)
			{
				config.Validate();
			}

			return newDelimiter ?? config.Delimiter;
		}
	}
}
