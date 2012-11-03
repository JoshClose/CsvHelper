// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvWriterConstructorTests
	{
		[Fact]
		public void InvalidParametersTest()
		{
			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvWriter( null );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				new CsvWriter( null, new CsvConfiguration() );
			} );

			Assert.Throws<ArgumentNullException>( () =>
			{
				using( var stream = new MemoryStream() )
				using( var writer = new StreamWriter( stream ) )
				{
					new CsvWriter( writer, null );
				}
			} );
		}

		[Fact]
		public void EnsureInternalsAreSetupWhenPasingWriterAndConfigTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			{
				var config = new CsvConfiguration();
				using( var csv = new CsvWriter( writer, config ) )
				{
					Assert.Same( config, csv.Configuration );
				}
			}
		}
	}
}
