// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Tests.Issues
{
	[TestClass]
	public class Issue920
	{
		[TestMethod]
		public void Test1()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,B");
				writer.WriteLine("1,one");
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.GetConstructor = type =>
					type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
					.OrderBy(c => c.GetParameters().Length)
					.First();
				csv.Configuration.IncludePrivateMembers = true;
				var records = csv.GetRecords<Sample>().ToList();

				Assert.AreEqual(2, records.Count);
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
