// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using Xunit;

namespace CsvHelper.Tests.Issues
{
	// https://github.com/JoshClose/CsvHelper/issues/1341
	// Line break in quoted field with LineBreakInQuotedFieldIsBadData = true
	// consumes data indefinitely until another quote is found.
	public class Issue1341
	{
		[Fact]
		public void Test1()
		{
			string csvString =
			"""
			"MemberNumber","MemberFullName","MemberFirstName","MemberLastName","AddressLine1","AddressLine2","City","State","ZipCode","PhoneNumber","EmailAddress","Gender","EffectiveDate","TerminationDate","Birthdate","ClientCode","GroupCode","PlanCode","MemberAffiliation"
			"GDD0000001","Good Member1","Good","Member1","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient"
			"BAD0000001","Missing Quote","Missing","Quote","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient
			"GDD0000002","Good Member2","Good","Member2","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient"
			"BAD0000002","Extra Quote","Extra","Quote","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient""
			"GDD0000003","Good Member3","Good","Member3","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient"
			"BAD0000003","Middle Quote","Mid"dle","Quote","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient"
			"GDD0000004","Good Member4","Good","Member4","","","","","","","email@test.com","F","1/15/2017","06/16/2019","03/05/1954","1","","1","Patient"
			""";

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
				BadDataFound = null
			};

			int numReads = 0;

			using (var sr = new StringReader(csvString))
			using (var csv = new CsvParser(sr, config))
			{
				while (csv.Read())
				{
					Assert.Equal(19, csv.Count);
					numReads++;
				}
			}

			Assert.Equal(8, numReads);
		}

		[Fact]
		public void Test2()
		{
			string csvString =
			"""
			Field1, Field2, Field3, Field4, Field5
			1,2,3, "Text 1","Text
			1,2,3, Text 1,Text
			1,2,3, "Text 1",Text"
			1,2,3, Text 1",Text
			1,2,3, Text 1,Text
			1,2,3, "Text 3","Text 4"
			""";

			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
				LineBreakInQuotedFieldIsBadData = true,
				BadDataFound = null
			};

			int numReads = 0;

			using (var sr = new StringReader(csvString))
			using (var csv = new CsvParser(sr, config))
			{
				while (csv.Read())
				{
					Assert.Equal(5, csv.Count);
					numReads++;
				}
			}

			Assert.Equal(7, numReads);
		}
	}
}
