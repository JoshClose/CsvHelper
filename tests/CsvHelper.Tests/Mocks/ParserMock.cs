// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;

namespace CsvHelper.Tests.Mocks
{
	public class ParserMock : IParser, IEnumerable<string[]>
	{
		private readonly Queue<string[]> records = new Queue<string[]>();
		private string[] record;
		private int row;

		public CsvContext Context { get; private set; }

		public IParserConfiguration Configuration { get; private set; }

		public int Count => record?.Length ?? 0;

		public string[] Record => record;

		public string RawRecord => string.Empty;

		public int Row => row;

		public int RawRow => row;

		public long ByteCount => 0;

		public long CharCount => 0;

		public string Delimiter => Configuration.Delimiter;

		public string this[int index] => record[index];

		public ParserMock() : this(new CsvConfiguration(CultureInfo.InvariantCulture)) { }

		public ParserMock(CsvConfiguration configuration)
		{
			Configuration = configuration;
			Context = new CsvContext(this);
		}

		public bool Read()
		{
			if (records.Count == 0)
			{
				return false;
			}

			row++;
			record = records.Dequeue();

			return true;
		}

		public Task<bool> ReadAsync()
		{
			row++;
			record = records.Dequeue();

			return Task.FromResult(records.Count > 0);
		}

		public void Dispose()
		{
		}

		#region Mock Methods

		public void Add(params string[] record)
		{
			records.Enqueue(record);
		}

		public IEnumerator<string[]> GetEnumerator()
		{
			return records.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion Mock Methods
	}
}
