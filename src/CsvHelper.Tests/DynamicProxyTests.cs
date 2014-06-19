using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class DynamicProxyTests
	{
		[TestMethod]
		public void Test()
		{
			var list = new List<TestClass>();
			var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
			for( var i = 0; i < 1; i++ )
			{
				var proxy = proxyGenerator.CreateClassProxy<TestClass>();
				proxy.Id = i + 1;
				proxy.Name = "name" + proxy.Id;
				list.Add( proxy );
			}

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<TestClassMap>();
				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = new StringBuilder();
				expected.AppendLine( "id,name" );
				expected.AppendLine( "1,name1" );

				Assert.AreEqual( expected.ToString(), data );
			}
		}

		public class TestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestClassMap : CsvClassMap<TestClass> 
		{
			public TestClassMap()
			{
				Map( m => m.Id ).Name( "id" );
				Map( m => m.Name ).Name( "name" );
			}
		}
	}
}
