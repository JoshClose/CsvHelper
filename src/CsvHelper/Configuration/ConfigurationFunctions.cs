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
		private static readonly char[] quoteChars = new char[] { '\r', '\n' };

		/// <summary>
		/// Throws a <see cref="ValidationException"/> if <paramref name="invalidHeaders"/> is not empty.
		/// </summary>
		public static void HeaderValidated(InvalidHeader[] invalidHeaders, CsvContext context)
		{
			if (invalidHeaders.Count() == 0)
			{
				return;
			}

			var errorMessage = new StringBuilder();
			foreach (var invalidHeader in invalidHeaders)
			{
				errorMessage.AppendLine($"Header with name '{string.Join("' or '", invalidHeader.Names)}'[{invalidHeader.Index}] was not found.");
			}

			var messagePostfix =
				$"If you are expecting some headers to be missing and want to ignore this validation, " +
				$"set the configuration {nameof(HeaderValidated)} to null. You can also change the " +
				$"functionality to do something else, like logging the issue.";
			errorMessage.AppendLine(messagePostfix);

			throw new HeaderValidationException(context, invalidHeaders, errorMessage.ToString());
		}

		/// <summary>
		/// Throws a <c>MissingFieldException</c>.
		/// </summary>
		public static void MissingFieldFound(string[] headerNames, int index, CsvContext context)
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof(MissingFieldFound)} to null.";

			// Get by index.

			if (headerNames == null || headerNames.Length == 0)
			{
				throw new MissingFieldException(context, $"Field at index '{index}' does not exist. {messagePostfix}");
			}

			// Get by name.

			var indexText = index > 0 ? $" at field index '{index}'" : string.Empty;

			if (headerNames.Length == 1)
			{
				throw new MissingFieldException(context, $"Field with name '{headerNames[0]}'{indexText} does not exist. {messagePostfix}");
			}

			throw new MissingFieldException(context, $"Field containing names '{string.Join("' or '", headerNames)}'{indexText} does not exist. {messagePostfix}");
		}

		/// <summary>
		/// Throws a <see cref="BadDataException"/>.
		/// </summary>
		public static void BadDataFound(CsvContext context)
		{
			throw new BadDataException(context, $"You can ignore bad data by setting {nameof(BadDataFound)} to null.");
		}

		/// <summary>
		/// Throws the given <paramref name="exception"/>.
		/// </summary>
		public static bool ReadingExceptionOccurred(CsvHelperException exception)
		{
			return true;
		}

		/// <summary>
		/// Returns true if the field contains a <see cref="IWriterConfiguration.QuoteString"/>,
		/// starts with a space, ends with a space, contains \r or \n, or contains
		/// the <see cref="ISerializerConfiguration.Delimiter"/>.
		/// </summary>
		/// <param name="field">The current field.</param>
		/// <param name="row">The current row.</param>
		/// <returns></returns>
		public static bool ShouldQuote(string field, IWriterRow row)
		{
			var config = row.Configuration;

			var shouldQuote = !string.IsNullOrEmpty(field) && 
			(
				field.Contains(config.Quote) // Contains quote
				|| field[0] == ' ' // Starts with a space
				|| field[field.Length - 1] == ' ' // Ends with a space
				|| field.IndexOfAny(quoteChars) > -1 // Contains chars that require quotes
				|| (config.Delimiter.Length > 0 && field.Contains(config.Delimiter)) // Contains delimiter
			);

			return shouldQuote;
		}

		/// <summary>
		/// Returns <c>false</c>.
		/// </summary>
		public static bool ShouldSkipRecord(string[] record)
		{
			return false;
		}

		/// <summary>
		/// Returns the <paramref name="header"/> as given.
		/// </summary>
		public static string PrepareHeaderForMatch(string header, int index)
		{
			return header;
		}

		/// <summary>
		/// Returns <c>true</c> if <paramref name="type"/>:
		/// 1. does not have a parameterless constructor
		/// 2. has a constructor
		/// 3. is not a user defined struct
		/// 4. is not an interface
		/// 5. TypeCode is not an Object.
		/// </summary>
		public static bool ShouldUseConstructorParameters(Type type)
		{
			return !type.HasParameterlessConstructor()
				&& type.HasConstructor()
				&& !type.IsUserDefinedStruct()
				&& !type.IsInterface
				&& Type.GetTypeCode(type) == TypeCode.Object;
		}

		/// <summary>
		/// Returns the type's constructor with the most parameters. 
		/// If two constructors have the same number of parameters, then
		/// there is no guarantee which one will be returned. If you have
		/// that situation, you should probably implement this function yourself.
		/// </summary>
		public static ConstructorInfo GetConstructor(Type type)
		{
			return type.GetConstructorWithMostParameters();
		}

		/// <summary>
		/// Returns the header name ran through <see cref="PrepareHeaderForMatch(string, int)"/>.
		/// If no header exists, property names will be Field1, Field2, Field3, etc.
		/// </summary>
		/// <param name="context">The <see cref="CsvContext"/>.</param>
		/// <param name="fieldIndex">The field index of the header to get the name for.</param>
		public static string GetDynamicPropertyName(CsvContext context, int fieldIndex)
		{
			if (context.Reader.HeaderRecord == null)
			{
				return $"Field{fieldIndex + 1}";
			}

			var header = context.Reader.HeaderRecord[fieldIndex];
			header = context.Reader.Configuration.PrepareHeaderForMatch(header, fieldIndex);

			return header;
		}
	}
}
