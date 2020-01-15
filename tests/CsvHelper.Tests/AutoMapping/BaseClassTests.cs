// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Text;

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
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
