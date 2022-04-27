// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;
using System.Collections;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Writing
{
	
	public class AnonymousTypesTests
	{
		[Fact]
		public void AnonymousIEnumerableTest()
		{
			using( var writer = new StringWriter() )
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
			{
				IEnumerable records = new ArrayList
				{
					new
					{
						Id = 1,
						Name = "one",
					}
				};

				csv.WriteRecords( records );
			}
		}
	}
}
