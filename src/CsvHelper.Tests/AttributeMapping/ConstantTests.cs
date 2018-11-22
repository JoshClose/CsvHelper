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
	public class ConstantTests
	{
		[TestMethod]
		public void ConstantTest()
		{
			using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<ConstantTestClass>().ToList();

				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("two", records[0].Name);
			}
		}

		private class ConstantTestClass
		{
			public int Id { get; set; }

			[Constant("two")]
			public string Name { get; set; }
		}
	}
}