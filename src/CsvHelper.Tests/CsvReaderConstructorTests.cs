// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderConstructorTests
	{
		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				Assert.AreSame( csv.Configuration, csv.Parser.Configuration );
			}
		}

		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderAndConfigurationTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader, new CsvConfiguration() ) )
			{
				Assert.AreSame( csv.Configuration, csv.Parser.Configuration );
			}
		}

		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingParserTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			{
				var parser = new CsvParser( reader );

				using( var csv = new CsvReader( parser ) )
				{
					Assert.AreSame( csv.Configuration, csv.Parser.Configuration );
					Assert.AreSame( parser, csv.Parser );
				}
			}
		}

		private class TestParser : IParser
		{
			private ReadingContext context;

			public IParserContext Context => context;

			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public TextReader TextReader { get; }

			public ICsvParserConfiguration Configuration => context.ParserConfiguration;

			public int FieldCount
			{
				get { throw new NotImplementedException(); }
			}

			public int RawRow { get; private set; }

			public string RawRecord { get; private set; }

			public string[] Read()
			{
				throw new NotImplementedException();
			}

			public long CharPosition
			{
				get { throw new NotImplementedException(); }
			}

			public long BytePosition { get; private set; }

			public int Row
			{
				get { throw new NotImplementedException(); }
			}

			public IFieldReader FieldReader
			{
				get
				{
					throw new NotImplementedException();
				}
			}
		}
	}
}
