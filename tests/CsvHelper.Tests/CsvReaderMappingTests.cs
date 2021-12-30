// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests
{

	public class CsvReaderMappingTests
	{
		[Fact]
		public void ReadWithConvertUsingTest()
		{
			var parserMock = new ParserMock
			{
				{ "int2", "string.3" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csvReader = new CsvReader(parserMock);
			// csvReader.Configuration.HeaderValidated = (isValid, headerNames, headerNameIndex, context) => {};
			csvReader.Context.RegisterClassMap<ConvertUsingClassMap>();

			var records = csvReader.GetRecords<MultipleNamesClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(1, records[0].IntColumn);
			Assert.Equal("one", records[0].StringColumn);
			Assert.Equal(2, records[1].IntColumn);
			Assert.Equal("two", records[1].StringColumn);
		}

		[Fact]
		public void ReadMultipleNamesTest()
		{
			var parserMock = new ParserMock
			{
				{ "int2", "string3" },
				{ "1", "one" },
				{ "2", "two" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<MultipleNamesClassMap>();

			var records = csvReader.GetRecords<MultipleNamesClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(1, records[0].IntColumn);
			Assert.Equal("one", records[0].StringColumn);
			Assert.Equal(2, records[1].IntColumn);
			Assert.Equal("two", records[1].StringColumn);
		}

		[Fact]
		public void ConvertUsingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
				{ "3", "4" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<ConvertUsingMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(3, records[0].IntColumn);
			Assert.Equal(7, records[1].IntColumn);
		}

		[Fact]
		public void ConvertUsingCovarianceTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<CovarianceClassMap>();

			var records = csvReader.GetRecords<CovarianceClass>().ToList();
		}

		[Fact]
		public void ConvertUsingBlockTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
				{ "3", "4" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<ConvertUsingBlockMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(3, records[0].IntColumn);
			Assert.Equal(7, records[1].IntColumn);
		}

		[Fact]
		public void ConvertUsingConstantTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
				{ "3", "4" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<ConvertUsingConstantMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(1, records[0].IntColumn);
			Assert.Equal(1, records[1].IntColumn);
		}

		[Fact]
		public void ReadSameNameMultipleTimesTest()
		{
			var parserMock = new ParserMock
			{
				{ "ColumnName", "ColumnName", "ColumnName" },
				{ "2", "3", "1" },
			};

			var csv = new CsvReader(parserMock);
			csv.Context.RegisterClassMap<SameNameMultipleTimesClassMap>();

			var records = csv.GetRecords<SameNameMultipleTimesClass>().ToList();

			Assert.NotNull(records);
			Assert.Single(records);
		}

		[Fact]
		public void ConvertUsingInstanceMethodTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
				{ "3", "4" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<ConvertUsingInstanceMethodMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(3, records[0].IntColumn);
			Assert.Equal(7, records[1].IntColumn);
		}

		[Fact]
		public void ConvertUsingStaticFunctionTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parserMock = new ParserMock(config)
			{
				{ "1", "2" },
				{ "3", "4" },
			};

			var csvReader = new CsvReader(parserMock);
			csvReader.Context.RegisterClassMap<ConvertUsingStaticFunctionMap>();

			var records = csvReader.GetRecords<TestClass>().ToList();

			Assert.NotNull(records);
			Assert.Equal(2, records.Count);
			Assert.Equal(3, records[0].IntColumn);
			Assert.Equal(7, records[1].IntColumn);
		}

		private class CovarianceClass
		{
			public int? Id { get; set; }
		}

		private sealed class CovarianceClassMap : ClassMap<CovarianceClass>
		{
			public CovarianceClassMap()
			{
				Map(m => m.Id).Convert(args => args.Row.GetField<int>(0));
			}
		}

		private class ContravarianceClass
		{
			public int Id { get; set; }
		}

		private class TestClass
		{
			public int IntColumn { get; set; }
		}

		private class SameNameMultipleTimesClass
		{
			public string Name1 { get; set; }

			public string Name2 { get; set; }

			public string Name3 { get; set; }
		}

		private sealed class SameNameMultipleTimesClassMap : ClassMap<SameNameMultipleTimesClass>
		{
			public SameNameMultipleTimesClassMap()
			{
				Map(m => m.Name1).Name("ColumnName").NameIndex(1);
				Map(m => m.Name2).Name("ColumnName").NameIndex(2);
				Map(m => m.Name3).Name("ColumnName").NameIndex(0);
			}
		}

		private class MultipleNamesClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class MultipleNamesClassMap : ClassMap<MultipleNamesClass>
		{
			public MultipleNamesClassMap()
			{
				Map(m => m.IntColumn).Name("int1", "int2", "int3");
				Map(m => m.StringColumn).Name("string1", "string2", "string3");
			}
		}


		private class ConstructorMappingClass
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }

			public ConstructorMappingClass(string stringColumn)
			{
				StringColumn = stringColumn;
			}
		}

		private sealed class ConvertUsingMap : ClassMap<TestClass>
		{
			public ConvertUsingMap()
			{
				Map(m => m.IntColumn).Convert(args => args.Row.GetField<int>(0) + args.Row.GetField<int>(1));
			}
		}

		private sealed class ConvertUsingBlockMap : ClassMap<TestClass>
		{
			public ConvertUsingBlockMap()
			{
				Map(m => m.IntColumn).Convert(args =>
			 {
				 var x = args.Row.GetField<int>(0);
				 var y = args.Row.GetField<int>(1);
				 return x + y;
			 });
			}
		}

		private sealed class ConvertUsingConstantMap : ClassMap<TestClass>
		{
			public ConvertUsingConstantMap()
			{
				Map(m => m.IntColumn).Convert(row => 1);
			}
		}

		private sealed class ConvertUsingClassMap : ClassMap<MultipleNamesClass>
		{
			public ConvertUsingClassMap()
			{
				Map(m => m.IntColumn).Name("int2");
				Map(m => m.StringColumn).Convert(args => args.Row.GetField("string.3"));
			}
		}

		private sealed class ConvertUsingInstanceMethodMap : ClassMap<TestClass>
		{
			public ConvertUsingInstanceMethodMap()
			{
				Map(m => m.IntColumn).Convert(ConvertFromStringFunction);
			}

			private int ConvertFromStringFunction(ConvertFromStringArgs args)
			{
				return args.Row.GetField<int>(0) + args.Row.GetField<int>(1);
			}
		}

		private sealed class ConvertUsingStaticFunctionMap : ClassMap<TestClass>
		{
			public ConvertUsingStaticFunctionMap()
			{
				Map(m => m.IntColumn).Convert(ConvertFromStringFunction);
			}

			private static int ConvertFromStringFunction(ConvertFromStringArgs args)
			{
				return args.Row.GetField<int>(0) + args.Row.GetField<int>(1);
			}
		}
	}
}
