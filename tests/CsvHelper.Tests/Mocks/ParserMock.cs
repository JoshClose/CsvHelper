// Copyright 2009-2020 Josh Close and Contributors
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

namespace CsvHelper.Tests.Mocks
{
	public class ParserMock : IParser, IEnumerable<string[]>
	{
		private readonly Queue<string[]> rows;
		private ReadingContext context;

		public ReadingContext Context => context;

		public IParserConfiguration Configuration { get; }

		public IFieldReader FieldReader
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public ParserMock()
		{
			context = new ReadingContext(new StringReader(string.Empty), new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture), false);
			rows = new Queue<string[]>();
		}

		public ParserMock(Queue<string[]> rows)
		{
			context = new ReadingContext(new StringReader(string.Empty), new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture), false);
			this.rows = rows;
		}

		public string[] Read()
		{
			context.Row++;
			return rows.Dequeue();
		}

		public Task<string[]> ReadAsync()
		{
			context.Row++;
			return Task.FromResult(rows.Dequeue());
		}

		public void Add(params string[] row)
		{
			rows.Enqueue(row);
		}

		public IEnumerator<string[]> GetEnumerator()
		{
			return rows.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Dispose()
		{
		}
	}
}
