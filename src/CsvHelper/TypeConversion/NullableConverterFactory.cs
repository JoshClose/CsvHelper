// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.TypeConversion;

/// <inheritdoc />
public class NullableConverterFactory : ITypeConverterFactory
{
	/// <inheritdoc />
	public bool CanCreate(Type type)
	{
		return (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
	}

	/// <inheritdoc />
	public bool Create(Type type, TypeConverterCache cache, out ITypeConverter typeConverter)
	{
		typeConverter = new NullableConverter(type, cache);

		return true;
	}
}
