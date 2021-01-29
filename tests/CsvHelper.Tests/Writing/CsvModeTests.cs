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
    public class CsvModeTests
    {
		[TestMethod]
		public void WriteField_EscapeMode_ContainsQuote_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\"b", true);
				csv.Flush();

				Assert.AreEqual("a\\\"b", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_NoEscapeMode_ContainsQuote_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\"b", true);
				csv.Flush();

				Assert.AreEqual("a\"b", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_EscapeMode_ContainsDelimiter_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a,b", true);
				csv.Flush();

				Assert.AreEqual("a\\,b", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_NoEscapeMode_ContainsDelimiter_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a,b", true);
				csv.Flush();

				Assert.AreEqual("a,b", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_EscapeMode_ContainsNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
				NewLine = "\n",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\nb", true);
				csv.Flush();

				Assert.AreEqual("a\\\nb", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_EscapeMode_Contains2CharNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.Escape,
				Escape = '\\',
				NewLine = "\r\n",
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\r\nb", true);
				csv.Flush();

				Assert.AreEqual("a\\\r\\\nb", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_NoEscapeMode_ContainsNewline_EscapesWithoutQuotingField()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = CsvMode.NoEscape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("a\r\nb", true);
				csv.Flush();

				Assert.AreEqual("a\r\nb", writer.ToString());
			}
		}
	}
}
