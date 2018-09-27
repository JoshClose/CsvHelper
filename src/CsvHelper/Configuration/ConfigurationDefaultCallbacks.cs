using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>Holds the default callback methods for delegate members of <c>CsvHelper.Configuration.Configuration</c>.</summary>
	public static class ConfigurationDefaultCallbacks
	{
		/// <summary>Throws <c>ValidationException</c> if <paramref name="isValid"/> is <c>false</c>.</summary>
		public static void HeaderValidated(Boolean isValid, string[] headerNames, int headerNameIndex, ReadingContext context)
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
		}

		/// <summary>Always throws a <c>MissingFieldException</c>.</summary>
		public static void MissingFieldFound(string[] headerNames, int index, ReadingContext context)
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof( MissingFieldFound )} to null.";

			if( headerNames != null && headerNames.Length > 0 )
			{
				throw new MissingFieldException( context, $"Field with names ['{string.Join( "', '", headerNames )}'] at index '{index}' does not exist. {messagePostfix}" );
			}

			throw new MissingFieldException( context, $"Field at index '{index}' does not exist. {messagePostfix}" );
		}

		/// <summary>Always throws a <c>BadDataException</c>.</summary>
		public static void BadDataFound(ReadingContext context)
		{
			throw new BadDataException( context, $"You can ignore bad data by setting {nameof( BadDataFound )} to null." );
		}

		/// <summary>Always re-throws exceptions wrapped in a new <c>CsvHelperException</c>.</summary>
		public static void ReadingExceptionOccurred(CsvHelperException exception)
		{
			// TODO: Don't simply `throw exception` because it erases the stack trace. Re-throw it by wrapping it in another exception:
			//throw new CsvHelperException( "Exception caught by " + nameof(ReadingExceptionOccurred) + " was unhandled.", exception );
			throw exception;
		}

		/// <summary>Always returns <c>false</c>.</summary>
		public static bool ShouldSkipRecord(string[] record)
		{
			return false;
		}

		/// <summary>Always returns <paramref name="header"/> verbatim.</summary>
		public static string PrepareHeaderForMatch(string header)
		{
			return header;
		}

		/// <summary>Returns <c>true</c> if <paramref name="type"/> is a class and has a non-parameterless constructor.</summary>
		public static bool ShouldUseConstructorParameters(Type type)
		{
			return !type.HasParameterlessConstructor()
				&& type.HasConstructor()
				&& !type.IsUserDefinedStruct()
				&& !type.IsInterface
				&& Type.GetTypeCode( type ) == TypeCode.Object;
		}

		/// <summary>Always returns the type's constructor with the most parameters. If two constructors have the same number of parameters then it is not specified which constructor will be used.</summary>
		public static ConstructorInfo GetConstructor(Type type)
		{
			return type.GetConstructorWithMostParameters();
		}
	}
}
