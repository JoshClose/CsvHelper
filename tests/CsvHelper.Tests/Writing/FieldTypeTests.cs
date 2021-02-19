using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class FieldTypeTests
    {
		[TestMethod]
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
				Assert.AreEqual(typeof(string), type);

				csv.WriteField(1);
				Assert.AreEqual(typeof(int), type);

				csv.WriteField(string.Empty);
				Assert.AreEqual(typeof(string), type);
			}
		}

		[TestMethod]
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
								Assert.AreEqual(typeof(int), args.FieldType);
								break;
							case 1:
								Assert.AreEqual(typeof(string), args.FieldType);
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
