// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class MultipleHeadersTests
	{
		[TestMethod]
		public void ReadWithoutMapTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("Y,Z");
				writer.WriteLine("two,2");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.AreEqual(1, csv.GetField<int>("A"));
				Assert.AreEqual("one", csv.GetField("B"));

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.AreEqual("two", csv.GetField("Y"));
				Assert.AreEqual(2, csv.GetField<int>("Z"));
			}
		}

		[TestMethod]
		public void ReadWithMapTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("Y,Z");
				writer.WriteLine("two,2");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<AlphaMap>();
				csv.Configuration.RegisterClassMap<OmegaMap>();

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var alphaRecord = csv.GetRecord<Alpha>();

				Assert.AreEqual(1, alphaRecord.A);
				Assert.AreEqual("one", alphaRecord.B);

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var omegaRecord = csv.GetRecord<Omega>();

				Assert.AreEqual("two", omegaRecord.Y);
				Assert.AreEqual(2, omegaRecord.Z);
			}
		}

		private class Alpha
		{
			public int A { get; set; }
			public string B { get; set; }
		}

		private class Omega
		{
			public string Y { get; set; }
			public int Z { get; set; }
		}

		private sealed class AlphaMap : ClassMap<Alpha>
		{
			public AlphaMap()
			{
				Map(m => m.A);
				Map(m => m.B);
			}
		}

		private sealed class OmegaMap : ClassMap<Omega>
		{
			public OmegaMap()
			{
				Map(m => m.Y);
				Map(m => m.Z);
			}
		}
	}
}
