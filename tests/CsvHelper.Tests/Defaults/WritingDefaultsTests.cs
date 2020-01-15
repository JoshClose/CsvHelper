// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Defaults
{
	[TestClass]
	public class WritingDefaultsTests
	{
		[TestMethod]
		public void EmptyFieldsOnNullReferencePropertyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<A>
				{
					new A
					{
						AId = 1,
					},
					new A
					{
						AId = 2,
						B = new B
						{
							BId = 3,
						},
					},
				};

				csv.Configuration.UseNewObjectForNullReferenceMembers = false;
				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords(records);

				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = "AId,BId,CId\r\n" +
							   "1,,\r\n" +
							   "2,3,0\r\n";
				Assert.AreEqual(expected, data);
			}
		}

		[TestMethod]
		public void DefaultFieldsOnNullReferencePropertyTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<A>
				{
					new A
					{
						AId = 1,
					},
					new A
					{
						AId = 2,
						B = new B
						{
							BId = 3,
						},
					},
				};

				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords(records);

				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = "AId,BId,CId\r\n" +
							   "1,0,0\r\n" +
							   "2,3,0\r\n";
				Assert.AreEqual(expected, data);
			}
		}

		private class A
		{
			public int AId { get; set; }

			public B B { get; set; }
		}

		private sealed class AMap : ClassMap<A>
		{
			public AMap()
			{
				AutoMap(CultureInfo.InvariantCulture);
				Map(m => m.AId).Default(1);
			}
		}

		public class B
		{
			public int BId { get; set; }
			public int CId { get; set; }
		}
	}
}
