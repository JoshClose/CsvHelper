// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
	public class DetectColumnCountChangesTests
	{
		[Fact]
		public void ConstructorAttributeTest()
		{
			Assert.True(CsvConfiguration.FromAttributes<FooTrue>(CultureInfo.InvariantCulture).DetectColumnCountChanges);
			Assert.False(CsvConfiguration.FromAttributes<FooFalse>(CultureInfo.InvariantCulture).DetectColumnCountChanges);
		}

		[DetectColumnCountChanges]
		private class FooTrue { }

		[DetectColumnCountChanges(false)]
		private class FooFalse { }
	}
}
