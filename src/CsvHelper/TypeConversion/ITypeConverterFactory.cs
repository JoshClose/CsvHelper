// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.TypeConversion;

/// <summary>
/// Produces <see cref="ITypeConverter"/> for the specified <see cref="System.Type"/>
/// </summary>
public interface ITypeConverterFactory
{
	/// <summary>
	/// Determines if the factory can create a type converter for the given type.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> to be checked</param>
	/// <returns><c>true</c> if the factory can create the type, otherwise <c>false</c>.</returns>
	bool CanCreate(Type type);

	/// <summary>
	/// Creates a type converter for the given type and assigns it to the given out typeConverter parameter.
	/// </summary>
	/// <param name="type">The type to create the converter for.</param>
	/// <param name="cache">The type converter cache.</param>
	/// <param name="typeConverter">The parameter to set the converter to.</param>
	/// <returns><c>true</c> if the converter should be added to the cache, otherwise <c>false</c>.</returns>
	bool Create(Type type, TypeConverterCache cache, out ITypeConverter typeConverter);
}
