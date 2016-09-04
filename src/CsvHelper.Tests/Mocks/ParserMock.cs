// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.Mocks
{
	public class ParserMock : ICsvParser, IEnumerable<string[]>
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
	    public int RawRow { get; }

	    public ParserMock()
		{
			Configuration = new CsvConfiguration();
			rows = new Queue<string[]>();
		}

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

		public void Add( params string[] row )
		{
			rows.Enqueue( row );
		}

		public IEnumerator<string[]> GetEnumerator()
		{
			return rows.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
