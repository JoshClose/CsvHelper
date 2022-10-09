using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class HeaderPrefixTests
	{
		[Fact]
		public void WriteHeader_PrefixCustom_WritesCustomPrefixesOwnLevelOnly()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteHeader<ACustom>();
				csv.Flush();

				Assert.Equal("AId,b_BId,c_CId", writer.ToString());
			}
		}

		[Fact]
		public void WriteHeader_PrefixInherit_WritesPrefixesForEachLevel()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteHeader<AInherit>();
				csv.Flush();

				Assert.Equal("AId,B.BId,B.C.CId", writer.ToString());
			}
		}

		[Fact]
		public void WriteHeader_PrefixNoInherit_WritesPrefixesOwnLevelOnly()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteHeader<ANoInherit>();
				csv.Flush();

				Assert.Equal("AId,B.BId,C.CId", writer.ToString());
			}
		}

		[Fact]
		public void WriteHeader_PrefixDefaultInherit_WritesPrefixesOwnLevelOnly()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteHeader<ADefaultInherit>();
				csv.Flush();

				Assert.Equal("AId,B.BId,C.CId", writer.ToString());
			}
		}

		[Fact]
		public void GetRecords_PrefixCustom_ReadsCustomHeader()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("AId,b_BId,c_CId");
			s.AppendLine("aid,bid,cid");
			using (var reader = new StringReader(s))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<ACustom>().ToList();

				Assert.Single(records);
				Assert.Equal("aid", records[0].AId);
				Assert.Equal("bid", records[0].B.BId);
				Assert.Equal("cid", records[0].B.C.CId);
			}
		}

		[Fact]
		public void GetRecords_PrefixInherit_ReadsInheritedHeader()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("AId,B.BId,B.C.CId");
			s.AppendLine("aid,bid,cid");
			using (var reader = new StringReader(s))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<AInherit>().ToList();

				Assert.Single(records);
				Assert.Equal("aid", records[0].AId);
				Assert.Equal("bid", records[0].B.BId);
				Assert.Equal("cid", records[0].B.C.CId);
			}
		}

		[Fact]
		public void GetRecords_PrefixNoInherit_ReadsNonInheritedHeader()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("AId,B.BId,C.CId");
			s.AppendLine("aid,bid,cid");
			using (var reader = new StringReader(s))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<ANoInherit>().ToList();

				Assert.Single(records);
				Assert.Equal("bid", records[0].B.BId);
				Assert.Equal("aid", records[0].AId);
				Assert.Equal("cid", records[0].B.C.CId);
			}
		}

		[Fact]
		public void GetRecords_PrefixDefaultInherit_ReadsNonInheritedHeader()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("AId,B.BId,C.CId");
			s.AppendLine("aid,bid,cid");
			using (var reader = new StringReader(s))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<ADefaultInherit>().ToList();

				Assert.Single(records);
				Assert.Equal("aid", records[0].AId);
				Assert.Equal("bid", records[0].B.BId);
				Assert.Equal("cid", records[0].B.C.CId);
			}
		}

		private class ACustom
		{
			public string AId { get; set; }
			[HeaderPrefix("b_")]
			public BCustom B { get; set; }
		}

		private class BCustom
		{
			public string BId { get; set; }
			[HeaderPrefix("c_")]
			public C C { get; set; }
		}

		private class AInherit
		{
			public string AId { get; set; }
			[HeaderPrefix(true)]
			public BInherit B { get; set; }
		}

		private class BInherit
		{
			public string BId { get; set; }
			[HeaderPrefix(true)]
			public C C { get; set; }
		}

		private class ANoInherit
		{
			public string AId { get; set; }
			[HeaderPrefix(false)]
			public BInherit B { get; set; }
		}

		private class BNoInherit
		{
			public string BId { get; set; }
			[HeaderPrefix(false)]
			public C C { get; set; }
		}

		private class ADefaultInherit
		{
			public string AId { get; set; }
			[HeaderPrefix]
			public BInherit B { get; set; }
		}

		private class BDefaultInherit
		{
			public string BId { get; set; }
			[HeaderPrefix]
			public C C { get; set; }
		}

		private class C
		{
			public string CId { get; set; }
		}
	}
}
