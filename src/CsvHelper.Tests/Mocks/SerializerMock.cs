// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.Mocks
{
	public class SerializerMock : ISerializer
	{
		private readonly List<string[]> records = new List<string[]>();
		private readonly bool throwExceptionOnWrite;

		public TextWriter TextWriter { get; }

		public ICsvSerializerConfiguration Configuration { get; }

		public List<string[]> Records
		{
			get { return records; }
		}

		public WritingContext Context { get; }

		public SerializerMock( bool throwExceptionOnWrite = false )
		{
			Context = new WritingContext( new StringWriter(), new CsvConfiguration(), false );
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

		public void WriteLine()
		{
		}

		public void Dispose()
		{
		}
	}
}
