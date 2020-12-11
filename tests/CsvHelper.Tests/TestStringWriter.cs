using System;
using System.IO;

namespace CsvHelper.Tests
{
	/// <summary>
	/// A <see cref="StreamWriter"/> class with configurable line ending for unit tests.
	/// </summary>
	public class TestStreamWriter : StreamWriter
	{
		private readonly string _newLine;

		public TestStreamWriter(Stream stream, string newLine = "\r\n") : base(stream)
		{
			_newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
		}

		public override void WriteLine(string value)
		{
			base.Write(value);
			base.Write(_newLine);
		}
	}
}
