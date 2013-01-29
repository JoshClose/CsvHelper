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
			return rows.Dequeue();
		}
	}
}
