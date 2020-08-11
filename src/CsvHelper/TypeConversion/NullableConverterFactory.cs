using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converter factory for nullable types
	/// </summary>
	public class NullableConverterFactory : ITypeConverterFactory
	{
		/// <summary>
		/// Produces <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type"><see cref="System.Type"/> we want <see cref="ITypeConverter"/> for</param>
		/// <param name="typeConverterCache"><see cref="TypeConverterCache"/> that is used for retrieving already exising type converters that are used to build the new one</param>
		/// <returns>Created <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/></returns>
		public ITypeConverter CreateTypeConverter(Type type, TypeConverterCache typeConverterCache)
		{
			return new NullableConverter(type, typeConverterCache);
		}

		/// <summary>
		/// Checks whether the type is handled by <see cref="NullableConverterFactory"/>.
		/// </summary>
		/// <param name="type"><see cref="System.Type"/> to be checked</param>
		/// <returns>Whether the type is handled by <see cref="NullableConverterFactory"/></returns>
		public bool Handles(Type type)
		{
			return (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
		}
	}
}
