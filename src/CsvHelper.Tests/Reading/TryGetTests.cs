﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldInvalidNameTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "one", "two" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.IsFalse( got );
			Assert.AreEqual( default( int ), field );
		}

		[TestMethod]
		public void TryGetFieldTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			int field;
			var got = reader.TryGetField( 0, out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
		public void TryGetFieldStrictTest()
		{
			var data1 = new[] { "One", "Two" };
			var data2 = new[] { "1", "2" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data1 );
			queue.Enqueue( data2 );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock ) { Configuration = { WillThrowOnMissingField = true } };
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			int field;
			var got = reader.TryGetField( "One", out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 1, field );
		}

		[TestMethod]
		public void TryGetFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an emptry string for a date.
			var data = new[] { " " };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			DateTime field;
			var got = reader.TryGetField( 0, out field );

			Assert.IsFalse( got );
			Assert.AreEqual( DateTime.MinValue, field );
		}

		[TestMethod]
		public void TryGetNullableFieldEmptyDate()
		{
			// DateTimeConverter.IsValid() doesn't work correctly
			// so we need to test and make sure that the conversion
			// fails for an emptry string for a date.
			var data = new[] { " " };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Read();

			DateTime? field;
			var got = reader.TryGetField( 0, out field );

			Assert.IsFalse( got );
			Assert.IsNull( field );
		}

		[TestMethod]
		public void TryGetDoesNotThrowWhenWillThrowOnMissingFieldIsEnabled()
		{
			var data = new[] { "1" };
			var queue = new Queue<string[]>();
			queue.Enqueue( data );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.WillThrowOnMissingField = true;
			reader.Read();
			string field;
			Assert.IsFalse( reader.TryGetField( "test", out field ) );
		}

		[TestMethod]
		public void TryGetFieldIndexTest()
		{
			var parserMock = new ParserMock
			{
				{ "One", "Two", "Two" },
				{ "1", "2", "3" }
			};
			var reader = new CsvReader( parserMock );
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			int field;
			var got = reader.TryGetField( "Two", 0, out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 2, field );

			got = reader.TryGetField( "Two", 1, out field );
			Assert.IsTrue( got );
			Assert.AreEqual( 3, field );
		}
	}
}
