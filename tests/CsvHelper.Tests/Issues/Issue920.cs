// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Tests.Issues
{
	
	public class Issue920
	{
		[Fact]
		public void Test1()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				GetConstructor = args =>
					args.ClassType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
					.OrderBy(c => c.GetParameters().Length)
					.First(),
				IncludePrivateMembers = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Sample>().ToList();

				Assert.Equal(2, records.Count);
			}
		}

		private class Sample
		{
			public int A { get; private set; }
			public string B { get; private set; }

			private Sample() { }

			public Sample(int a, string b)
			{
				A = a;
				B = b;
			}
		}
	}
}
