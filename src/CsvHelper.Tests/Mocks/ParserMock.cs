// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.Mocks
{
	public class ParserMock : ICsvParser
	{
		private readonly Queue<string[]> rows;

		public void Dispose()
		{
		}

		public CsvConfiguration Configuration { get; private set; }
		public int FieldCount { get; private set; }
		public long CharPosition { get; private set; }
		public long BytePosition { get; private set; }
		public int Row { get; private set; }
		public string RawRecord { get; private set; }

		public ParserMock( Queue<string[]> rows )
		{
			Configuration = new CsvConfiguration();
			this.rows = rows;
		}

		public string[] Read()
		{
		    Row++;
			return rows.Dequeue();
		}
	}
}
