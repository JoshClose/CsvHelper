using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>Holds the default callback methods for delegate members of <c>CsvHelper.Configuration.Configuration</c>.</summary>
	public static class ConfigurationFunctions
	{
		/// <summary>
		/// Throws a <see cref="ValidationException"/> if <paramref name="isValid"/> is <c>false</c>.
		/// </summary>
		public static void HeaderValidated(bool isValid, string[] headerNames, int headerNameIndex, ReadingContext context)
		{
			if (isValid)
			{
				return;
			}

			var message =
				$"Header matching ['{string.Join("', '", headerNames)}'] names at index {headerNameIndex} was not found. " +
				$"If you are expecting some headers to be missing and want to ignore this validation, " +
				$"set the configuration {nameof(HeaderValidated)} to null. You can also change the " +
				$"functionality to do something else, like logging the issue.";

			throw new ValidationException(context, message);
		}

		/// <summary>
		/// Throws a <c>MissingFieldException</c>.
		/// </summary>
		public static void MissingFieldFound(string[] headerNames, int index, ReadingContext context)
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof(MissingFieldFound)} to null.";

			if (headerNames != null && headerNames.Length > 0)
			{
				throw new MissingFieldException(context, $"Field with names ['{string.Join("', '", headerNames)}'] at index '{index}' does not exist. {messagePostfix}");
			}

			throw new MissingFieldException(context, $"Field at index '{index}' does not exist. {messagePostfix}");
		}

		/// <summary>
		/// Throws a <see cref="BadDataException"/>.
		/// </summary>
		public static void BadDataFound(ReadingContext context)
		{
			throw new BadDataException(context, $"You can ignore bad data by setting {nameof(BadDataFound)} to null.");
		}

		/// <summary>
		/// Throws the given <paramref name="exception"/>.
		/// </summary>
		public static void ReadingExceptionOccurred(CsvHelperException exception)
		{
			throw exception;
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
		public static string PrepareHeaderForMatch(string header)
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
	}
}