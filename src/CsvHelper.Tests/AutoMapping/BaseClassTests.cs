using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class BaseClassTests
	{
		[TestMethod]
		public void EnsureChildNotWrittenWhenListIsParent()
		{
			var record = new Child
			{
				ChildProp = "child",
				ParentProp = "parent"
			};
			Parent[] records = { record };

			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.WriteRecords( records );
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine( "ParentProp" );
				expected.AppendLine( "parent" );

				Assert.AreEqual( expected.ToString(), reader.ReadToEnd() );
			}
		}

		private class Parent
		{
			public string ParentProp { get; set; }
		}

		private class Child : Parent
		{
			public string ChildProp { get; set; }
		}
	}
}
