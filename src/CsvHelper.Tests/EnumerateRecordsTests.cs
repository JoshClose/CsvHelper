using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	[TestClass]
    public class EnumerateRecordsTests
    {
		[TestMethod]
		public void BasicTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HeaderValidatedCallback = null;
				csv.Configuration.MissingFieldFoundCallback = null;

				var record = new Basic
				{
					Id = -1,
					Name = "-one"
				};

				var count = 1;
				foreach( var r in csv.EnumerateRecords( record ) )
				{
					if( count == 1 )
					{
						Assert.AreEqual( 1, r.Id );
						Assert.AreEqual( "one", r.Name );
					}
					else if( count == 2 )
					{
						Assert.AreEqual( 2, r.Id );
						Assert.AreEqual( "two", r.Name );
					}

					count++;
				}
			}
		}

		[TestMethod]
        public void UnUsedPropertyTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HeaderValidatedCallback = null;
				csv.Configuration.MissingFieldFoundCallback = null;

				var record = new UnUsedProperty
				{
					Id = -1,
					Name = "-one",
					UnUsed = "nothing",
				};

				var count = 1;
				foreach( var r in csv.EnumerateRecords( record ) )
				{
					if( count == 1 )
					{
						Assert.AreEqual( 1, r.Id );
						Assert.AreEqual( "one", r.Name );
						Assert.AreEqual( "nothing", r.UnUsed );
					}
					else if( count == 2 )
					{
						Assert.AreEqual( 2, r.Id );
						Assert.AreEqual( "two", r.Name );
						Assert.AreEqual( "nothing", r.UnUsed );
					}

					count++;
				}
			}
		}

		[TestMethod]
		public void ReferenceTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HeaderValidatedCallback = null;
				csv.Configuration.MissingFieldFoundCallback = null;

				var record = new HasReferences
				{
					Id = -1,
					Reference = new Reference
					{
						Name = "one"
					}
				};

				var count = 1;
				foreach( var r in csv.EnumerateRecords( record ) )
				{
					if( count == 1 )
					{
						Assert.AreEqual( 1, r.Id );
						Assert.AreEqual( "one", r.Reference.Name );
					}
					else if( count == 2 )
					{
						Assert.AreEqual( 2, r.Id );
						Assert.AreEqual( "two", r.Reference.Name );
					}

					count++;
				}
			}
		}

		private class Basic
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private class UnUsedProperty
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public string UnUsed { get; set; }
		}

		public class HasReferences
		{
			public int Id { get; set; }

			public Reference Reference { get; set; }
		}

		public class Reference
		{
			public string Name { get; set; }
		}
    }
}
