using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Produces <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/>
	/// </summary>
	public interface ITypeConverterFactory
	{
		/// <summary>
		/// Checks whether the type is handled by this <see cref="ITypeConverterFactory"/>.
		/// </summary>
		/// <param name="type"><see cref="System.Type"/> to be checked</param>
		/// <returns>Whether the type is handled by this <see cref="ITypeConverterFactory"/></returns>
		bool Handles(Type type);

		/// <summary>
		/// Produces <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type"><see cref="System.Type"/> we want <see cref="ITypeConverter"/> for</param>
		/// <param name="typeConverterCache"><see cref="TypeConverterCache"/> that is used for retrieving already exising type converters that are used to build the new one</param>
		/// <returns>Created <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/></returns>
		ITypeConverter CreateTypeConverter(Type type, TypeConverterCache typeConverterCache);
	}
}
