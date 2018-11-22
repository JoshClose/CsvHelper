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
	public class BooleanValuesTests
	{
		[TestMethod]
		public void BooleanValuesTest()
		{
			using (var reader = new StringReader("IsTrue,IsFalse\r\ntrue,false\r\n"))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				var records = csv.GetRecords<BooleanValuesTestClass>().ToList();
				Assert.AreEqual(true, records[0].IsTrue);
				Assert.AreEqual(false, records[0].IsFalse);
			}
		}

		private class BooleanValuesTestClass
		{
			[BooleanTrueValues("true")]
			public bool? IsTrue { get; set; }

			[BooleanFalseValues("false")]
			public bool? IsFalse { get; set; }
		}
	}
}