using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class AnonymousTypesTests
    {
		[TestMethod]
		public void AnonymouseIEnumerableTest()
		{
			using( var writer = new StringWriter() )
			using( var csv = new CsvWriter( writer ) )
			{
				IEnumerable records = new ArrayList
				{
					new
					{
						Id = 1,
						Name = "one",
					}
				};

				csv.WriteRecords( records );
			}
		}
	}
}
