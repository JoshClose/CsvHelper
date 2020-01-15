// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Exceptions
{
	[TestClass]
	public class ExceptionTests
	{
		[TestMethod]
		public void NoDefaultConstructorTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader(reader, CultureInfo.InvariantCulture) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,2" );
				writer.WriteLine( "3,4" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					csv.Configuration.HeaderValidated = null;
					var list = csv.GetRecords<NoDefaultConstructor>().ToList();
					Assert.Fail();
				}
				catch( ReaderException )
				{
				}
			}
		}

		private class NoDefaultConstructor
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public NoDefaultConstructor( int id, string name )
			{
				Id = id;
				Name = name;
			}
		}
	}
}
