// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;

namespace CsvHelper.Tests
{
	/// <summary>
	/// A <see cref="StreamWriter"/> class with configurable line ending for unit tests.
	/// </summary>
	public class TestStreamWriter : StreamWriter
	{
		private readonly string newLine;

		public TestStreamWriter(Stream stream, string newLine = "\r\n") : base(stream)
		{
			this.newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
		}

		public override void WriteLine(string value)
		{
			base.Write(value);
			base.Write(newLine);
		}
	}
}
