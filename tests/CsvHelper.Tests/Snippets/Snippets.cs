// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

class Snippets
{
	#region Poco

	public class Foo
	{
		public int Id { get; set; }
		public string Name { get; set; }
	}

	#endregion

	#region IndexPoco

	public class FooWithIndex
	{
		[Index(0)] public int Id { get; set; }

		[Index(1)] public string Name { get; set; }
	}

	#endregion

	#region NamePoco

	public class FooWithName
	{
		[Name("id")] public int Id { get; set; }

		[Name("name")] public string Name { get; set; }
	}

	#endregion

	#region ClassMap

	public class FooMap : ClassMap<Foo>
	{
		public FooMap()
		{
			Map(m => m.Id).Name("id");
			Map(m => m.Name).Name("name");
		}
	}

	#endregion

	#region IndexAndNameMap

	public class IndexAndNameMap : ClassMap<Foo>
	{
		public IndexAndNameMap()
		{
			Map(m => m.Id).Index(0).Name("id");
			Map(m => m.Name).Index(1).Name("name");
		}
	}

	#endregion

	public void Reading()
	{
		#region Reading

		using var reader = new StreamReader(@"path\to\file.csv");
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
		var records = csv.GetRecords<Foo>();

		#endregion
	}

	public void PrepareHeaderForMatch()
	{
		#region PrepareHeaderForMatch

		using var reader = new StreamReader(@"path\to\file.csv");
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture)
		{
			Configuration =
			{
				PrepareHeaderForMatch = (string header, int index) => header.ToLower()
			}
		};
		var records = csv.GetRecords<Foo>();

		#endregion
	}

	public void HasHeaderRecord()
	{
		#region HasHeaderRecord

		using var reader = new StreamReader(@"path\to\file.csv");
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture)
		{
			Configuration =
			{
				HasHeaderRecord = false
			}
		};
		var records = csv.GetRecords<Foo>();

		#endregion
	}

	public void Writing()
	{
		#region ListForWriting

		var records = new List<Foo>
		{
			new Foo {Id = 1, Name = "one"},
			new Foo {Id = 2, Name = "two"},
		};

		#endregion

		#region Writing

		using var writer = new StreamWriter(@"path\to\file.csv");
		using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
		csv.WriteRecords((IEnumerable) records);

		#endregion
	}

	public void RegisterClassMap()
	{
		#region RegisterClassMap

		using var reader = new StreamReader(@"path\to\file.csv");
		using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
		csv.Configuration.RegisterClassMap<FooMap>();
		var records = csv.GetRecords<Foo>();

		#endregion
	}
}
