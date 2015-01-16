using System;
using CsvHelper.TypeConversion;

namespace CsvHelperSample
{
	/// <summary>
	/// The type converter that can be used to read a column value into an object.
	/// </summary>
	public class CustomTypeTypeConverter : ITypeConverter
	{
		public string ConvertToString(TypeConverterOptions options, object value)
		{
			var obj = (CustomType)value;
			return obj.ToString();
		}

		public object ConvertFromString(TypeConverterOptions options, string text)
		{
			return Enum.Parse(typeof(CustomType), text);
		}

		public bool CanConvertFrom(Type type)
		{
			throw new NotImplementedException();
		}

		public bool CanConvertTo(Type type)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// This represents a non-primitive, custom type that requires manual conversion.
	/// </summary>
	public enum CustomType
	{
		Unknown = 0,

		First = 1,
		Second = 2,
		Third = 3
	}
}