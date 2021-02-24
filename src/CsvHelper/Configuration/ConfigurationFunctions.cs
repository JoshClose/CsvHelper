// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

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
			throw new BadDataException(args.Context, $"You can ignore bad data by setting {nameof(BadDataFound)} to null.");
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
		/// Returns <c>false</c>.
		/// </summary>
		public static bool ShouldSkipRecord(ShouldSkipRecordArgs args)
		{
			return false;
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
		/// 3. is not a user defined struct
		/// 4. is not an interface
		/// 5. TypeCode is not an Object.
		/// </summary>
		public static bool ShouldUseConstructorParameters(ShouldUseConstructorParametersArgs args)
		{
			return !args.ParameterType.HasParameterlessConstructor()
				&& args.ParameterType.HasConstructor()
				&& !args.ParameterType.IsUserDefinedStruct()
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
			var prepareHeaderForMatchArgs = new PrepareHeaderForMatchArgs
			{
				FieldIndex = args.FieldIndex,
				Header = header,
			};
			header = args.Context.Reader.Configuration.PrepareHeaderForMatch(prepareHeaderForMatchArgs);

			return header;
		}
	}
}
