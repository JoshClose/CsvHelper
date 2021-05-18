using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Parsing
{
    public class DetectDelimiterTests
    {
		[Fact]
        public void Parse_TextHasCommas_DetectsComma()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(",", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_TextHasSemicolons_DetectsSemicolon()
		{
			var s = new StringBuilder();
			s.Append("Id;Name\r\n");
			s.Append("1;one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(";", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_TextHasPipes_DetectsPipe()
		{
			var s = new StringBuilder();
			s.Append("Id|Name\r\n");
			s.Append("1|one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("|", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_TextHasTabs_DetectsTab()
		{
			var s = new StringBuilder();
			s.Append("Id\tName\r\n");
			s.Append("1\tone\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("\t", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_DelimiterValuesEmpty_ThrowsException()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
				DetectDelimiterValues = new string[0],
			};
			using (var reader = new StringReader(s.ToString()))
			{ 
				Assert.Throws<ConfigurationException>(() => new CsvParser(reader, config));
			}
		}

		[Fact]
		public void Parse_EqualAmountOfDelimiters_DetectsFirstInDelimiterValuesList()
		{
			var s = new StringBuilder();
			s.Append(";;,,\t\t||\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(",", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_TextHas2CharDelimiter_DetectsDelimiter()
		{
			var s = new StringBuilder();
			s.Append("Id,,Name\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
				DetectDelimiterValues = new[] { ",," },
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(",,", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_TextHasCommas_ParsesRows()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				var row = parser.Read();

				Assert.Equal("Id", parser[0]);
				Assert.Equal("Name", parser[1]);

				row = parser.Read();

				Assert.Equal("1", parser[0]);
				Assert.Equal("one", parser[1]);
			}
		}

		[Fact]
		public void Parse_TextHasRegularCharDelimiter_DetectsDelimiter()
		{
			var s = new StringBuilder();
			s.Append("IdþName\r\n");
			s.Append("1þone\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
				DetectDelimiterValues = new[] { "þ" }
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("þ", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_MultipleLines_DetectsDelimiterFromFirstLineOnly()
		{
			var s = new StringBuilder();
			s.Append("Id;Name\r\n");
			s.Append("1,2,3,4;5,6,7,8\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
				DetectDelimiterValues = new[] { ",", ";" }
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(";", parser.Delimiter);
			}
		}

		[Fact]
		public void Parse_NoDelimiter_DoesNotDetect()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiter = true,
				DetectDelimiterValues = new[] { ";" }
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("`", parser.Delimiter);
			}
		}
	}
}
