using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
				ObjectResolver.Current = new ObjectResolver(type => true, (type, args) => {
					Assert.Fail();
					return null;
				});

				var s = new StringBuilder();
				s.Append("Id,Name\r\n");
				s.Append("1,one\r\n");
				using (var reader = new StringReader(s.ToString()))
				using (var csv = new CsvReader(reader))
				{
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
				ObjectResolver.Current = new ObjectResolver(type => true, (type, args) => {
					Assert.Fail();
					return null;
				});

				using (var writer = new StringWriter())
				using (var csv = new CsvWriter(writer))
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
