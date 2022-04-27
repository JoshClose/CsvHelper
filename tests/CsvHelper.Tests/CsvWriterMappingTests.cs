// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvWriterMappingTests
	{
		[Fact]
		public void WriteMultipleNamesTest()
		{
			var records = new List<MultipleNamesClass>
			{
				new MultipleNamesClass { IntColumn = 1, StringColumn = "one" },
				new MultipleNamesClass { IntColumn = 2, StringColumn = "two" }
			};

			string csv;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csvWriter.Context.RegisterClassMap<MultipleNamesClassMap>();
				csvWriter.WriteRecords(records);

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = string.Empty;
			expected += "int1,string1\r\n";
			expected += "1,one\r\n";
			expected += "2,two\r\n";

			Assert.NotNull(csv);
			Assert.Equal(expected, csv);
		}

		[Fact]
		public void SameNameMultipleTimesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var records = new List<SameNameMultipleTimesClass>
				{
					new SameNameMultipleTimesClass
					{
						Name1 = "1",
						Name2 = "2",
						Name3 = "3"
					}
				};
				csv.Context.RegisterClassMap<SameNameMultipleTimesClassMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				var expected = "ColumnName,ColumnName,ColumnName\r\n1,2,3\r\n";
				Assert.Equal(expected, text);
			}
		}

		[Fact]
		public void ConvertUsingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Context.RegisterClassMap<ConvertUsingMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.Equal("Converted1\r\n", result);
		}

		[Fact]
		public void ConvertUsingBlockTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Context.RegisterClassMap<ConvertUsingBlockMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.Equal("Converted1\r\n", result);
		}

		[Fact]
		public void ConvertUsingConstantTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Context.RegisterClassMap<ConvertUsingConstantMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.Equal("Constant\r\n", result);
		}


		[Fact]
		public void ConvertUsingNullTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";",
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				var records = new List<MultipleNamesClass>
				{
					new MultipleNamesClass { IntColumn = 1, StringColumn = "test" }
				};

				csv.Context.RegisterClassMap<ConvertUsingNullMap>();
				csv.WriteRecords(records);

				Assert.Equal(";test\r\n", writer.ToString());
			}
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

		private class TestClass
		{
			public int IntColumn { get; set; }
		}

		private sealed class ConvertUsingMap : ClassMap<TestClass>
		{
			public ConvertUsingMap()
			{
				Map(m => m.IntColumn).Convert(args => $"Converted{args.Value.IntColumn}");
			}
		}

		private sealed class ConvertUsingBlockMap : ClassMap<TestClass>
		{
			public ConvertUsingBlockMap()
			{
				Map(m => m.IntColumn).Convert(args =>
				{
					var x = "Converted";
					x += args.Value.IntColumn;
					return x;
				});
			}
		}

		private sealed class ConvertUsingConstantMap : ClassMap<TestClass>
		{
			public ConvertUsingConstantMap()
			{
				Map(m => m.IntColumn).Convert(m => "Constant");
			}
		}

		private sealed class ConvertUsingNullMap : ClassMap<MultipleNamesClass>
		{
			public ConvertUsingNullMap()
			{
				Map(m => m.IntColumn).Convert(m => (string)null);
				Map(m => m.StringColumn).Convert(args => args.Value.StringColumn);
			}
		}
	}
}
