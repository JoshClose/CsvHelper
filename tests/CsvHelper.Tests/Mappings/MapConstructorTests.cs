// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Mappings
{
	[TestClass]
	public class MapConstructorTests
	{
		[TestMethod]
		public void NoConstructor()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader(reader, CultureInfo.InvariantCulture) )
			{
				try
				{
					csv.Configuration.RegisterClassMap<TestMap>();
					Assert.Fail();
				}
				catch( InvalidOperationException ex )
				{
					Assert.AreEqual( "No public parameterless constructor found.", ex.Message );
				}
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			private TestMap( string test )
			{
				Map( m => m.Id );
				Map( m => m.Name );
			}
		}
	}
}
