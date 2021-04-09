using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	
    public class FieldTypeTests
    {
		[Fact]
        public void WriteField_ShouldQuote_HasCorrectFieldType()
		{
			Type type = null;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = args =>
				{
					type = args.FieldType;
					return ConfigurationFunctions.ShouldQuote(args);
				},
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(string.Empty);
				Assert.Equal(typeof(string), type);

				csv.WriteField(1);
				Assert.Equal(typeof(int), type);

				csv.WriteField(string.Empty);
				Assert.Equal(typeof(string), type);
			}
		}

		[Fact]
		public void WriteRecords_ShouldQuote_HasCorrectFieldType()
		{
			var records = new List<Foo>
			{
				new Foo { Id = 1, Name = "one" },
			};
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = args =>
				{
					if (args.Row.Row > 1)
					{
						switch (args.Row.Index)
						{
							case 0:
								Assert.Equal(typeof(int), args.FieldType);
								break;
							case 1:
								Assert.Equal(typeof(string), args.FieldType);
								break;
						}
					}

					return ConfigurationFunctions.ShouldQuote(args);
				},
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(records);
			}
		}

		private class Foo
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
