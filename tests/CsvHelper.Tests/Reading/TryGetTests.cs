// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class TryGetTests
	{
		[TestMethod]
		public void TryGetFieldInvalidIndexTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var queue = new Queue<string[]>();
			queue.Enqueue(data1);
			queue.Enqueue(data2);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Read();

			var got = reader.TryGetField(0, out int field);
			Assert.IsFalse(got);
			Assert.AreEqual(default(int), field);
		}

		[TestMethod]
		public void TryGetFieldInvalidNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var queue = new Queue<string[]>();
			queue.Enqueue(data1);
			queue.Enqueue(data2);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();

			var got = reader.TryGetField("One", out int field);
			Assert.IsFalse(got);
			Assert.AreEqual(default(int), field);
		}

		[TestMethod]
		public void TryGetFieldTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue(data1);
			queue.Enqueue(data2);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(0, out int field);
			Assert.IsTrue(got);
			Assert.AreEqual(1, field);
		}

		[TestMethod]
		public void TryGetFieldStrictTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue(data1);
			queue.Enqueue(data2);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField("One", out int field);
			Assert.IsTrue(got);
			Assert.AreEqual(1, field);
		}

		[TestMethod]
		public void TryGetFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an empty string for a date.
			var data = new[] { " " };
			var queue = new Queue<string[]>();
			queue.Enqueue(data);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			var got = reader.TryGetField(0, out DateTime field);

			Assert.IsFalse(got);
			Assert.AreEqual(DateTime.MinValue, field);
		}

		[TestMethod]
		public void TryGetNullableFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an empty string for a date.
			var data = new[] { " " };
			var queue = new Queue<string[]>();
			queue.Enqueue(data);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			var got = reader.TryGetField(0, out DateTime? field);

			Assert.IsFalse(got);
			Assert.IsNull(field);
		}

		[TestMethod]
		public void TryGetDoesNotThrowWhenWillThrowOnMissingFieldIsEnabled()
		{
			var data = new[] { "1" };
			var queue = new Queue<string[]>();
			queue.Enqueue(data);
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			var reader = new CsvReader(parserMock);
			reader.Configuration.MissingFieldFound = null;
			reader.Read();
			reader.ReadHeader();
			Assert.IsFalse(reader.TryGetField("test", out string field));
		}

		[TestMethod]
		public void TryGetFieldIndexTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two", "Two" },
				{ "1", "2", "3" }
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField("Two", 0, out int field);
			Assert.IsTrue(got);
			Assert.AreEqual(2, field);

			got = reader.TryGetField("Two", 1, out field);
			Assert.IsTrue(got);
			Assert.AreEqual(3, field);
		}

		[TestMethod]
		public void TryGetMissingDateTimeFieldTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "Name" },
				{ "1" },
				null
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(typeof(DateTime), "Name", out object field);

			Assert.IsFalse(got);
			Assert.AreEqual(DateTime.MinValue, field);
		}

		[TestMethod]
		public void TryGetMissingDateTimeOffsetFieldTest()
		{
			var parserMock = new ParserMock
			{
				{ "Id", "DateTime" },
				{ "1" },
				null
			};
			var reader = new CsvReader(parserMock);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			var got = reader.TryGetField(typeof(DateTimeOffset), "DateTime", out object field);

			Assert.IsFalse(got);
			Assert.AreEqual(DateTimeOffset.MinValue, field);
		}
	}
}
