// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class TrimTests
	{
		[TestMethod]
		public void OutsideStartTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a,b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideStartNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideStartSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c,d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideStartSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c,d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideEndTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "a  ,b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideEndNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "a  ,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideEndSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "a b c  ,d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideEndSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "a b c  ,d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideBothTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a  ,b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideBothNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a  ,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideBothSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c  ,d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideBothSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c  ,d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesStartTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a\",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesStartNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a\",b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesStartSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a b c\",d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesStartSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a b c\",d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesEndTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a\"  ,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesEndNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a\"  ,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesEndSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a b c\"  ,d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesEndSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a b c\"  ,d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a\"  ,b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a\"  ,b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a b c\"  ,d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"a b c\"  ,d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c  ,  d e f  \r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldMultipleRecordsNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  a b c  ,  d e f  ";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a\",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a\",b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c\",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c\",b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldDelimiterInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\" a ,b c\",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a ,b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldDelimiterInFieldSmallBufferNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
				BufferSize = 1,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\" a ,b c\",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a ,b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesEndTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a  \",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesEndNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a  \",b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesEndSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a b c  \",d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesEndSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"a b c  \",d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a  \",b\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a  \",b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c  \",d\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c  \",d";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c  \",\"  d e f  \"\r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldMultipleRecordsNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "\"  a b c  \",\"  d e f  \"";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideAndInsideQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"  a b c  \"  ,  \"  d e f  \"  \r\n";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideAndInsideQuotesNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "  \"  a b c  \"  ,  \"  d e f  \"  ";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b c", parser[0]);
				Assert.AreEqual("d e f", parser[1]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesNoSpacesNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "abc";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("abc", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void OutsideQuotesNoSpacesHasSpaceInFieldNoNewlineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				var line = "a b";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("a b", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideNoSpacesQuotesFieldHasEscapedQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			var line = "\"a \"\"b\"\" c\"";
			using (var reader = new StringReader(line))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual("a \"b\" c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesFieldHasEscapedQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			var line = "\" a \"\"b\"\" c \"\r\n";
			using (var reader = new StringReader(line))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual("a \"b\" c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesFieldHasEscapedQuotesNoNewLineTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.InsideQuotes,
			};
			var line = "\" a \"\"b\"\" c \"";
			using (var reader = new StringReader(line))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();

				Assert.AreEqual("a \"b\" c", parser[0]);
				Assert.AreEqual(line, parser.RawRecord.ToString());
			}
		}

		[TestMethod]
		public void ReadingTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("A,B");
				writer.WriteLine("  \"  a b c  \"  ,  \"  d e f  \"  ");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<dynamic>().ToList();

				var record = records[0];
				Assert.AreEqual("a b c", record.A);
				Assert.AreEqual("d e f", record.B);
			}
		}
	}
}
