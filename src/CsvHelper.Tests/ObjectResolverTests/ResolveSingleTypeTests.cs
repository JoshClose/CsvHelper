using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.ObjectResolverTests
{
	[TestClass]
    public class ResolverSingleTypeTests
    {
		[TestCleanup]
		public void Cleanup()
		{
			ObjectResolver.Current = new ObjectResolver(type => true, ReflectionHelper.CreateInstanceWithoutContractResolver);
		}

		[TestMethod]
		public void Test()
		{
			ObjectResolver.Current = new ObjectResolver(CanResolve, Resolve);

			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
				null
			};

			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<A>().ToList();

				Assert.AreEqual(1, records.Count);
			}
		}

		private bool CanResolve(Type type)
		{
			return type == typeof(A);
		}

		private object Resolve(Type type, object[] args)
		{
			return new A();
		}

		private class A
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
    }
}
