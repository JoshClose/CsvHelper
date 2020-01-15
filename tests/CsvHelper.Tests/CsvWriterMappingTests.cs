// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterMappingTests
	{
		[TestMethod]
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
				csvWriter.Configuration.Delimiter = ",";
				csvWriter.Configuration.RegisterClassMap<MultipleNamesClassMap>();
				csvWriter.WriteRecords(records);

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = string.Empty;
			expected += "int1,string1\r\n";
			expected += "1,one\r\n";
			expected += "2,two\r\n";

			Assert.IsNotNull(csv);
			Assert.AreEqual(expected, csv);
		}

		[TestMethod]
		public void SameNameMultipleTimesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<SameNameMultipleTimesClass>
				{
					new SameNameMultipleTimesClass
					{
						Name1 = "1",
						Name2 = "2",
						Name3 = "3"
					}
				};
				csv.Configuration.RegisterClassMap<SameNameMultipleTimesClassMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				var text = reader.ReadToEnd();
				var expected = "ColumnName,ColumnName,ColumnName\r\n1,2,3\r\n";
				Assert.AreEqual(expected, text);
			}
		}

		[TestMethod]
		public void ConvertUsingTest()
		{
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<ConvertUsingMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.AreEqual("Converted1\r\n", result);
		}

		[TestMethod]
		public void ConvertUsingBlockTest()
		{
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<ConvertUsingBlockMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.AreEqual("Converted1\r\n", result);
		}

		[TestMethod]
		public void ConvertUsingConstantTest()
		{
			string result;
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				var records = new List<TestClass>
				{
					new TestClass { IntColumn = 1 }
				};

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<ConvertUsingConstantMap>();
				csv.WriteRecords(records);
				writer.Flush();
				stream.Position = 0;

				result = reader.ReadToEnd();
			}

			Assert.AreEqual("Constant\r\n", result);
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
				Map(m => m.IntColumn).ConvertUsing(m => $"Converted{m.IntColumn}");
			}
		}

		private sealed class ConvertUsingBlockMap : ClassMap<TestClass>
		{
			public ConvertUsingBlockMap()
			{
				Map(m => m.IntColumn).ConvertUsing(m =>
			 {
				 var x = "Converted";
				 x += m.IntColumn;
				 return x;
			 });
			}
		}

		private sealed class ConvertUsingConstantMap : ClassMap<TestClass>
		{
			public ConvertUsingConstantMap()
			{
				Map(m => m.IntColumn).ConvertUsing(m => "Constant");
			}
		}
	}
}
