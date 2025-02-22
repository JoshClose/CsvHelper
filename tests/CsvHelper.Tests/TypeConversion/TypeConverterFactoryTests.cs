// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class TypeConverterFactoryTests
	{
		[Fact]
		public void ReadTypeConverterGenericInt()
		{
			var input = """
			            MaybeNumber
			            23

			            """;

			using var cr = new CsvReader(new StringReader(input), CultureInfo.InvariantCulture);
			cr.Context.TypeConverterCache.AddConverter(new MyOptionTypeFactory.OptionConverter<int>());
			var firstRow = cr.GetRecords<RecordWithGenerics>().First();
			Assert.Equal(new Option<int>(23), firstRow.MaybeNumber);
		}

		[Fact]
		public void WriteTypeConverterGenericInt()
		{
			var expected = """
			               MaybeNumber
			               42

			               """;

			var stringWriter = new StringWriter();
			using var cw = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
			cw.Context.TypeConverterCache.AddConverter(new MyOptionTypeFactory.OptionConverter<int>());
			cw.WriteRecords(new[]
			{
				new RecordWithGenerics(new Option<int>(42))
			});
			Assert.Equal(expected, stringWriter.ToString());
		}

		[Fact]
		public void ReadTypeConverterFactory()
		{
			var input = """
			            MaybeNumber
			            23

			            """;

			using var cr = new CsvReader(new StringReader(input), CultureInfo.InvariantCulture);
			cr.Context.TypeConverterCache.AddConverterFactory(new MyOptionTypeFactory());
			var firstRow = cr.GetRecords<RecordWithGenerics>().First();
			Assert.Equal(new Option<int>(23), firstRow.MaybeNumber);
		}

		[Fact]
		public void WriteTypeConverterFactory()
		{
			var expected = """
			               MaybeNumber
			               42

			               """;

			var stringWriter = new StringWriter();
			using var cw = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
			cw.Context.TypeConverterCache.AddConverterFactory(new MyOptionTypeFactory());
			cw.WriteRecords(new[]
			{
				new RecordWithGenerics(new Option<int>(42))
			});
			Assert.Equal(expected, stringWriter.ToString());
		}

		public readonly record struct Option<T> : IEnumerable<T>
		{
			public bool IsPresent { get; }
			private readonly T value;

			internal Option(T value)
			{
				IsPresent = true;
				this.value = value;
			}

			public IEnumerator<T> GetEnumerator()
			{
				if (IsPresent) yield return value;
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		private class MyOptionTypeFactory : ITypeConverterFactory
		{
			public bool CanCreate(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Option<>);

			public bool Create(Type type, TypeConverterCache cache, out ITypeConverter typeConverter)
			{
				var wrappedType = type.GetGenericArguments().Single();
				typeConverter = Activator.CreateInstance(typeof(OptionConverter<>).MakeGenericType(wrappedType)) as ITypeConverter ?? throw new NullReferenceException();

				return true;
			}

			internal class OptionConverter<T> : TypeConverter<Option<T>>
			{
				public override string? ConvertToString(Option<T> value, IWriterRow row, MemberMapData memberMapData)
				{
					var wrappedTypeConverter = row.Context.TypeConverterCache.GetConverter<T>();

					return value.IsPresent ? wrappedTypeConverter.ConvertToString(value.Single(), row, memberMapData) : "";
				}

				public override Option<T> ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
				{
					var wrappedTypeConverter = row.Context.TypeConverterCache.GetConverter<T>();

					return text == ""
						? new Option<T>()
						: new Option<T>((T)wrappedTypeConverter.ConvertFromString(text, row, memberMapData)!);
				}
			}
		}

		private record RecordWithGenerics(Option<int> MaybeNumber);
	}
}
