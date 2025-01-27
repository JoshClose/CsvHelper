// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Linq;

namespace CsvHelper.Tests.ObjectResolverTests
{
	
	public class ResolverSingleTypeTests
	{
		[Fact]
		public void Test()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
			};

			var originalResolver = ObjectResolver.Current;

			try
			{
				using (var csv = new CsvReader(parser))
				{
					ObjectResolver.Current = new ObjectResolver(CanResolve, Resolve);
					var records = csv.GetRecords<A>().ToList();

					Assert.Single(records);
				}
			}
			finally
			{
				ObjectResolver.Current = originalResolver;
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

			public string? Name { get; set; }
		}
	}
}
