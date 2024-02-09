using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts values to and from strings.
	/// </summary>
	public abstract class TypeConverterGeneric<T> : ITypeConverter
	{
		/// <summary>
		/// Converts the string to a (T) value.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The value created from the string.</returns>
		public abstract T ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData);

		/// <summary>
		/// Converts the value to a string.
		/// </summary>
		/// <param name="value">The value to convert to a string.</param>
		/// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
		/// <returns>The string representation of the value.</returns>
		public abstract string ConvertToString(T value, IWriterRow row, MemberMapData memberMapData);

		object ITypeConverter.ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
			ConvertFromString(text, row, memberMapData);

		string ITypeConverter.ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) =>
			value is T v
				? ConvertToString(v, row, memberMapData)
				: throw new System.InvalidCastException();
	}
}
