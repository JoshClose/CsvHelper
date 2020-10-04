// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	internal static class Enumerable
	{
		internal static IEnumerable<T> Generate<T>(Func<T> generator)
		{
			while (true)
				yield return generator();
		}

		internal static IEnumerable<T> GenerateWhile<T>(Func<bool> checker, Func<T> generator)
		{
			while (checker())
				yield return generator();
		}

	}

	[TestClass]
	public class MultipleGetRecordsTests
	{
		[TestMethod]
		public void Blah()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = Enumerable.GenerateWhile(csv.Read, csv.GetRecord<Test>).ToList();

				var position = stream.Position;
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = position;

				records = Enumerable.GenerateWhile(csv.Read, csv.GetRecord<Test>).ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(2, records[0].Id);
				Assert.AreEqual("two", records[0].Name);
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
