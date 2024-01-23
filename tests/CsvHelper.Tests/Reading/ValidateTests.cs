// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CsvHelper.Tests.Reading
{

	public class ValidateTests
	{
		[Fact]
		public void GenericValidateTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine(",one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<GenericValidateMap>();
				Assert.Throws<FieldValidationException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		[Fact]
		public void NonGenericValidateTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine(",one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<NonGenericValidateMap>();
				Assert.Throws<FieldValidationException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		[Fact]
		public void LogInsteadTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,");
				writer.Flush();
				stream.Position = 0;

				var logger = new StringBuilder();
				csv.Context.RegisterClassMap(new LogInsteadMap(logger));
				csv.GetRecords<Test>().ToList();

				var expected = new StringBuilder();
				expected.AppendLine("Field '' is not valid!");

				Assert.Equal(expected.ToString(), logger.ToString());
			}
		}

		[Fact]
		public void CustomExceptionTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MissingFieldFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine(",one");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<CustomExceptionMap>();
				Assert.Throws<CustomException>(() => csv.GetRecords<Test>().ToList());
			}
		}

		[Fact]
		public void ValidateMessageTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("Id,Name");
			s.AppendLine("1,one");
			using (var reader = new StringReader(s))
			using (var csv = new CsvReader(reader, config))
			{
				csv.Context.RegisterClassMap<ValidationMessageMap>();
				var exception = Assert.Throws<FieldValidationException>(() => csv.GetRecords<Test>().ToList());
				Assert.StartsWith("Field 'one' was not foo.", exception.Message);
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class GenericValidateMap : ClassMap<Test>
		{
			public GenericValidateMap()
			{
				Map(m => m.Id).Validate(args => !string.IsNullOrEmpty(args.Field));
				Map(m => m.Name);
			}
		}

		private sealed class NonGenericValidateMap : ClassMap<Test>
		{
			public NonGenericValidateMap()
			{
				AutoMap(System.Globalization.CultureInfo.InvariantCulture);
				foreach (var memberMap in MemberMaps)
				{
					Map(typeof(Test), memberMap.Data.Member).Validate(args => !string.IsNullOrEmpty(args.Field));
				}
			}
		}

		private sealed class LogInsteadMap : ClassMap<Test>
		{
			public LogInsteadMap(StringBuilder logger)
			{
				Map(m => m.Id);
				Map(m => m.Name).Validate(args =>
				{
					var isValid = !string.IsNullOrEmpty(args.Field);
					if (!isValid)
					{
						logger.AppendLine($"Field '{args.Field}' is not valid!");
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

		private class ValidationMessageMap : ClassMap<Test>
		{
			public ValidationMessageMap()
			{
				Map(m => m.Id);
				Map(m => m.Name).Validate(args => args.Field == "foo", args => $"Field '{args.Field}' was not foo.");
			}
		}
	}
}
