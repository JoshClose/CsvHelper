using System;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts objects to and from strings.
	/// </summary>
	public interface ITypeConverter
	{
		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <returns>The string representation of the object.</returns>
		string ConvertToString( object value );

		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="value">The object to convert to a string.</param>
		/// <returns>The string representation of the object.</returns>
		string ConvertToString( CultureInfo culture, object value );

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		object ConvertFromString( string text );

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		object ConvertFromString( CultureInfo culture, string text );

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		bool CanConvertFrom( Type type );

		/// <summary>
		/// Determines whether this instance [can convert to] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert to] the specified type; otherwise, <c>false</c>.
		/// </returns>
		bool CanConvertTo( Type type );
	}
}
