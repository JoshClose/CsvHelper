// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class ValidateTests
	{
		[TestMethod]
		public void ValidateTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine(",one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MissingFieldFound = null;
				csv.Configuration.RegisterClassMap<ValidateMap>();
				Assert.ThrowsException<FieldValidationException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		[TestMethod]
		public void LogInsteadTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,");
				writer.Flush();
				stream.Position = 0;

				var logger = new StringBuilder();
				csv.Configuration.MissingFieldFound = null;
				csv.Configuration.RegisterClassMap(new LogInsteadMap(logger));
				csv.GetRecords<Test>().ToList();

				var expected = new StringBuilder();
				expected.AppendLine("Field '' is not valid!");

				Assert.AreEqual(expected.ToString(), logger.ToString());
			}
		}

		[TestMethod]
		public void CustomExceptionTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine(",one");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.MissingFieldFound = null;
				csv.Configuration.RegisterClassMap<CustomExceptionMap>();
				Assert.ThrowsException<CustomException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class ValidateMap : ClassMap<Test>
		{
			public ValidateMap()
			{
				Map(m => m.Id).Validate(field => !string.IsNullOrEmpty(field));
				Map(m => m.Name);
			}
		}

		private sealed class LogInsteadMap : ClassMap<Test>
		{
			public LogInsteadMap(StringBuilder logger)
			{
				Map(m => m.Id);
				Map(m => m.Name).Validate(field =>
			 {
				 var isValid = !string.IsNullOrEmpty(field);
				 if (!isValid)
				 {
					 logger.AppendLine($"Field '{field}' is not valid!");
				 }

				 return true;
			 });
			}
		}

		private sealed class CustomExceptionMap : ClassMap<Test>
		{
			public CustomExceptionMap()
			{
				Map(m => m.Id).Validate(field => throw new CustomException());
				Map(m => m.Name);
			}
		}

		private class CustomException : CsvHelperException
		{
		}
	}
}
