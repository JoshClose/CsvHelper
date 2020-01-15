// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests.Issues
{
	[TestClass]
	public class Issue1073
	{
		[TestMethod]
		public void GetFieldTest()
		{
			var originalResolver = ObjectResolver.Current;
			try
			{
				ObjectResolver.Current = new ObjectResolver(type => true, (type, args) =>
				{
					Assert.Fail();
					return null;
				});

				var s = new StringBuilder();
				s.Append("Id,Name\r\n");
				s.Append("1,one\r\n");
				using (var reader = new StringReader(s.ToString()))
				using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
				{
					csv.Configuration.Delimiter = ",";
					csv.Read();
					csv.ReadHeader();
					while (csv.Read())
					{
						csv.GetField<int>("Id");
						csv.GetField<string>("Name");
					}
				}
			}
			finally
			{
				ObjectResolver.Current = originalResolver;
			}
		}

		[TestMethod]
		public void WriteFieldTest()
		{
			var originalResolver = ObjectResolver.Current;
			try
			{
				ObjectResolver.Current = new ObjectResolver(type => true, (type, args) =>
				{
					Assert.Fail();
					return null;
				});

				using (var writer = new StringWriter())
				using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
				{
					csv.WriteField(1);
					csv.WriteField("one");
					csv.NextRecord();
				}
			}
			finally
			{
				ObjectResolver.Current = originalResolver;
			}
		}
	}
}
