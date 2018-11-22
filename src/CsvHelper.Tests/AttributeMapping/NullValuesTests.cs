using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AttributeMapping
{
	[TestClass]
	public class NullValuesTests
	{
		[TestMethod]
		public void NullValuesTest()
		{
			using (var reader = new StringReader("Id,Name\r\nNULL,null\r\n"))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<NullValuesTestClass>().ToList();
				Assert.IsNull(records[0].Id);
				Assert.IsNull(records[0].Name);
			}
		}

		private class NullValuesTestClass
		{
			[NullValues("NULL")]
			public int? Id { get; set; }

			[NullValues("null")]
			public string Name { get; set; }
		}
	}
}