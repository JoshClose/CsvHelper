using System;
using System.Collections.Generic;
using System.IO;

namespace CsvHelper
{
	public class CsvParser : ICsvParser
	{
		private StreamReader reader;

		public CsvParser( StreamReader reader )
		{
			this.reader = reader;
		}

		public IList<string> Read()
		{
			throw new NotImplementedException();
		}
	}
}
