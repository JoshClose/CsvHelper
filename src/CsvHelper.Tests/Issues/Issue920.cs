using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
			using (var csv = new CsvReader(reader))
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