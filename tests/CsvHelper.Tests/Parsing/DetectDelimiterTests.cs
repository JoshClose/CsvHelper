// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
        public void GetDelimiter_TextHasCommas_DetectsComma()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
			};
			Assert.Equal(",", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_TextHasSemicolons_DetectsSemicolon()
		{
			var s = new StringBuilder();
			s.Append("Id;Name\r\n");
			s.Append("1;one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal(";", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
			}
		}

		[Fact]
		public void GetDelimiter_TextHasPipes_DetectsPipe()
		{
			var s = new StringBuilder();
			s.Append("Id|Name\r\n");
			s.Append("1|one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("|", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
			}
		}

		[Fact]
		public void GetDelimiter_TextHasTabs_DetectsTab()
		{
			var s = new StringBuilder();
			s.Append("Id\tName\r\n");
			s.Append("1\tone\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.Equal("\t", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
			}
		}

		[Fact]
		public void GetDelimiter_EqualAmountOfDelimiters_DetectsFirstInDelimiterValuesList()
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

				Assert.Equal(",", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
			}
		}

		[Fact]
		public void GetDelimiter_TextHas2CharDelimiter_DetectsDelimiter()
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

				Assert.Equal(",,", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
			}
		}

		[Fact]
		public void GetDelimiter_TextHasRegularCharDelimiter_DetectsDelimiter()
		{
			var s = new StringBuilder();
			s.Append("IdþName\r\n");
			s.Append("1þone\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiterValues = new[] { "þ" }
			};
			Assert.Equal("þ", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_MultipleLines_DetectsDelimiterThatIsOnEveryLine()
		{
			var s = new StringBuilder();
			s.Append("Id;Name\r\n");
			s.Append("1,2,3,4;5,6,7,8\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiterValues = new[] { ",", ";" }
			};
			Assert.Equal(";", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_NoDelimiter_DoesNotDetect()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = "`",
				DetectDelimiterValues = new[] { ";" }
			};
			Assert.Equal("`", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_CulturesSeparatorOccursLessButIsOnEveryLine_CulturesSeparatorIsDetected()
		{
			var s = new StringBuilder();
			s.Append("1;2,3;4\r\n");
			s.Append("5;6,7;8\r\n");
			s.Append("9;10,11;12\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			Assert.Equal(CultureInfo.InvariantCulture.TextInfo.ListSeparator, ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_CulturesSeparatorOccursLessAndIsOnFirstLine_CulturesSeparatorIsNotDetected()
		{
			var s = new StringBuilder();
			s.Append("1;2,3;4\r\n");
			s.Append("5;6;7;8\r\n");
			s.Append("9;10,11;12\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			Assert.NotEqual(CultureInfo.InvariantCulture.TextInfo.ListSeparator, ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_CulturesSeparatorOccursLessAndHasSingleLine_CulturesSeparatorIsNotDetected()
		{
			var s = new StringBuilder();
			s.Append("1;2,3;4\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			Assert.NotEqual(CultureInfo.InvariantCulture.TextInfo.ListSeparator, ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_CulturesSeparatorOccursLessAndHas2LinesAndIsOnEveryLine_CulturesSeparatorIsNotDetected()
		{
			var s = new StringBuilder();
			s.Append("1;2,3;4\r\n");
			s.Append("5;6,7;8\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			Assert.NotEqual(CultureInfo.InvariantCulture.TextInfo.ListSeparator, ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_CulturesSeparatorOccursLessAndIsOnSecondLine_CulturesSeparatorIsDetected()
		{
			var s = new StringBuilder();
			s.Append("1;2;3;4\r\n");
			s.Append("5;6,7;8\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			Assert.Equal(";", ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config)));
		}

		[Fact]
		public void GetDelimiter_TextHasLF_NewLineIsCRLF_DetectsDelimiter()
		{
			var s = new StringBuilder();
			s.Append("name;num;date\nLily;1,005.25;2021-02-03\nJack;3.5;2021-02-04");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
			};
			var delimeter = ConfigurationFunctions.GetDelimiter(new Delegates.GetDelimiterArgs(s.ToString(), config));
			Assert.Equal(";", delimeter);
		}

		[Fact]
		public void CsvParserConstructor_DelimiterValuesEmpty_ThrowsException()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\r\n");
			s.Append("1,one\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				DetectDelimiter = true,
				DetectDelimiterValues = new string[0],
			};
			using (var reader = new StringReader(s.ToString()))
			{
				Assert.Throws<ConfigurationException>(() => new CsvParser(reader, config));
			}
		}

		[Fact]
		public void Read_TextHasCommas_ParsesRows()
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
	}
}
