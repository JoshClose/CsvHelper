using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.Mocks
{
	public class SerializerMock : ICsvSerializer
	{
		private readonly List<string[]> records = new List<string[]>();
		private readonly bool throwExceptionOnWrite;

		public CsvConfiguration Configuration { get; private set; }

		public List<string[]> Records
		{
			get { return records; }
		}

		public SerializerMock( bool throwExceptionOnWrite = false )
		{
			Configuration = new CsvConfiguration();
			this.throwExceptionOnWrite = throwExceptionOnWrite;
		}

		public void Write( string[] record )
		{
			if( throwExceptionOnWrite )
			{
				throw new Exception( "Mock Write exception." );
			}

			records.Add( record );
		}

		public void Dispose()
		{
		}
	}
}
