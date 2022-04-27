// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Reading
{
	
	public class ConstructorParametersTests
	{
		[Fact]
		public void ValueTypesParamsMatchPropsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<ValueTypesParamsMatchProps>().ToList();

				Assert.Single(records);

				var record = records[0];

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		[Fact]
		public void ValueTypesParamsDontMatchPropsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args.Header)
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<ValueTypesParamsDontMatchProps>().ToList();

				Assert.Single(records);

				var record = records[0];

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		[Fact]
		public void MultipleConstructorsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args.Header)
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<MultipleConstructors>().ToList();

				Assert.Single(records);

				var record = records[0];

				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		[Fact]
		public void UseDifferentConstructorTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args.Header),
				GetConstructor = args => args.ClassType.GetConstructors().First(),
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<MultipleConstructors>().ToList();

				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Null(record.Name);
			}
		}

		[Fact]
		public void UseDifferentConstructorWhenDefaultIsAvailableTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args.Header),
				ShouldUseConstructorParameters = args =>
					!args.ParameterType.IsUserDefinedStruct()
					&& !args.ParameterType.IsInterface
					&& Type.GetTypeCode(args.ParameterType) == TypeCode.Object,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<MultipleConstructorsWithDefault>().ToList();

				Assert.Single(records);

				var record = records[0];
				Assert.Equal(1, record.Id);
				Assert.Equal("one", record.Name);
			}
		}

		private class ValueTypesParamsMatchProps
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public ValueTypesParamsMatchProps(int Id, string Name)
			{
				this.Id = Id;
				this.Name = Name;
			}
		}

		private class ValueTypesParamsDontMatchProps
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public ValueTypesParamsDontMatchProps(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private class MultipleConstructors
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public MultipleConstructors(int id)
			{
				Id = id;
			}

			public MultipleConstructors(int id, string name)
			{
				Id = id;
				Name = name;
			}

			public MultipleConstructors(string name)
			{
				Name = name;
			}
		}

		private class MultipleConstructorsWithDefault
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public MultipleConstructorsWithDefault() { }

			public MultipleConstructorsWithDefault(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}
	}
}
