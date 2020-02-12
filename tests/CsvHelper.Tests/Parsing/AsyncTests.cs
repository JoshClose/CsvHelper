using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class AsyncTests
	{
		[TestMethod]
		public void ProcessSpacesAsyncTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new ThrowsIfSyncParser(reader, CultureInfo.InvariantCulture))
			{
				var line = "\"a b c  \",d\r\n";
				parser.Configuration.Delimiter = ",";
				writer.Write(line);
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.ReadAsync().Result;

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual(line, parser.Context.RawRecord);
			}
		}

		private class ThrowsIfSyncParser : CsvParser
		{
			public ThrowsIfSyncParser(IFieldReader fieldReader) : base(fieldReader)
			{
			}

			public ThrowsIfSyncParser(TextReader reader, CultureInfo culture) : base(reader, culture)
			{
			}

			public ThrowsIfSyncParser(TextReader reader, CsvConfiguration configuration) : base(reader, configuration)
			{
			}

			public ThrowsIfSyncParser(TextReader reader, CultureInfo culture, bool leaveOpen) : base(reader, culture, leaveOpen)
			{
			}

			public ThrowsIfSyncParser(TextReader reader, CsvConfiguration configuration, bool leaveOpen) : base(reader, configuration, leaveOpen)
			{
			}

			public override string[] Read() => throw new InvalidOperationException("Synchronous operations are disallowed.");
			protected override bool ReadSpaces() => throw new InvalidOperationException("Synchronous operations are disallowed.");
		}
	}
}
