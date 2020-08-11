using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.TypeConversion
{
	internal class NullableConverterFactory : ITypeConverterFactory
	{
		public ITypeConverter CreateTypeConverter(Type type, TypeConverterCache typeConverterCache)
		{
			return new NullableConverter(type, typeConverterCache);
		}

		public bool Handles(Type type)
		{
			return (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
		}
	}
}
