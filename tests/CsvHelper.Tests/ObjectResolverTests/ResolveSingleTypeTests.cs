// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

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
