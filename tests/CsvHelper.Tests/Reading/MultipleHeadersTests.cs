// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	
	public class MultipleHeadersTests
	{
		[Fact]
		public void ReadWithoutMapTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("Y,Z");
				writer.WriteLine("two,2");
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.Equal(1, csv.GetField<int>("A"));
				Assert.Equal("one", csv.GetField("B"));

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.Equal("two", csv.GetField("Y"));
				Assert.Equal(2, csv.GetField<int>("Z"));
			}
		}

		[Fact]
		public void ReadWithMapTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("Y,Z");
				writer.WriteLine("two,2");
				writer.Flush();
				stream.Position = 0;

				csv.Context.RegisterClassMap<AlphaMap>();
				csv.Context.RegisterClassMap<OmegaMap>();

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var alphaRecord = csv.GetRecord<Alpha>();

				Assert.Equal(1, alphaRecord.A);
				Assert.Equal("one", alphaRecord.B);

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var omegaRecord = csv.GetRecord<Omega>();

				Assert.Equal("two", omegaRecord.Y);
				Assert.Equal(2, omegaRecord.Z);
			}
		}

		private class Alpha
		{
			public int A { get; set; }
			public string? B { get; set; }
		}

		private class Omega
		{
			public string? Y { get; set; }
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
