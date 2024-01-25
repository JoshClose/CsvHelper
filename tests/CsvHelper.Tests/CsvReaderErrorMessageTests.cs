// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class CsvReaderErrorMessageTests
	{
		public CsvReaderErrorMessageTests()
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
		}

		[Fact]
		public void FirstColumnEmptyFirstRowErrorWithNoHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				csvReader.Context.RegisterClassMap<Test1Map>();
				writer.WriteLine(",one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch (TypeConverterException ex)
				{
					Assert.Equal(1, ex.Context.Parser.Row);
					Assert.Equal(0, ex.Context.Reader.CurrentIndex);
				}
			}
		}

		[Fact]
		public void FirstColumnEmptySecondRowErrorWithHeader()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				csvReader.Context.RegisterClassMap<Test1Map>();
				writer.WriteLine("IntColumn,StringColumn");
				writer.WriteLine("1,one");
				writer.WriteLine(",two");
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch (TypeConverterException ex)
				{
					Assert.Equal(3, ex.Context.Parser.Row);
					Assert.Equal(0, ex.Context.Reader.CurrentIndex);
				}
			}
		}

		[Fact]
		public void FirstColumnEmptyErrorWithHeaderAndCommentRowTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				AllowComments = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				csvReader.Context.RegisterClassMap<Test1Map>();
				writer.WriteLine("IntColumn,StringColumn");
				writer.WriteLine("# comment");
				writer.WriteLine();
				writer.WriteLine(",one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch (TypeConverterException ex)
				{
					Assert.Equal(4, ex.Context.Parser.Row);
					Assert.Equal(0, ex.Context.Reader.CurrentIndex);
				}
			}
		}

		[Fact]
		public void FirstColumnErrorTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csvReader.Context.RegisterClassMap<Test1Map>();
				writer.WriteLine("IntColumn,StringColumn");
				writer.WriteLine();
				writer.WriteLine("one,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch (TypeConverterException ex)
				{
					Assert.Equal(3, ex.Context.Parser.Row);
					Assert.Equal(0, ex.Context.Reader.CurrentIndex);
				}
			}
		}

		[Fact]
		public void SecondColumnEmptyErrorTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csvReader.Context.RegisterClassMap<Test2Map>();
				writer.WriteLine("StringColumn,IntColumn");
				writer.WriteLine("one,");
				writer.WriteLine("two,2");
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test2>().ToList();
					throw new Exception();
				}
				catch (TypeConverterException ex)
				{
					Assert.Equal(2, ex.Context.Parser.Row);
					Assert.Equal(1, ex.Context.Reader.CurrentIndex);
				}
			}
		}

		[Fact]
		public void Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csvReader = new CsvReader(reader, config))
			{
				writer.WriteLine("1,9/24/2012");
				writer.Flush();
				stream.Position = 0;

				try
				{
					csvReader.Context.RegisterClassMap<Test3Map>();
					var records = csvReader.GetRecords<Test3>().ToList();
				}
				catch (ReaderException)
				{
					// Should throw this exception.
				}
			}
		}

		private class Test1
		{
			public int IntColumn { get; set; }

			public string StringColumn { get; set; }
		}

		private sealed class Test1Map : ClassMap<Test1>
		{
			public Test1Map()
			{
				Map(m => m.IntColumn).Index(0);
				Map(m => m.StringColumn).Index(1);
			}
		}

		private class Test2
		{
			public string StringColumn { get; set; }

			public int IntColumn { get; set; }
		}

		private sealed class Test2Map : ClassMap<Test2>
		{
			public Test2Map()
			{
				Map(m => m.StringColumn);
				Map(m => m.IntColumn);
			}
		}

		private class Test3
		{
			public int Id { get; set; }

			public DateTime CreationDate { get; set; }

			public string Description { get; set; }
		}

		private sealed class Test3Map : ClassMap<Test3>
		{
			public Test3Map()
			{
				Map(m => m.Id).Index(0);
				Map(m => m.CreationDate).Index(1);
				Map(m => m.Description).Index(2);
			}
		}
	}
}
