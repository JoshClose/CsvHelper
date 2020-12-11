using System;
using System.Text;

namespace CsvHelper.Tests
{
	/// <summary>
	/// A <see cref="StringBuilder"/> like class with configurable line ending for unit tests.
	/// </summary>
	public class TestStringBuilder
	{
		private readonly string _newLine;
		private readonly StringBuilder _builder;

		public TestStringBuilder(string newLine)
		{
			_newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
			_builder = new StringBuilder();
		}

		public TestStringBuilder AppendLine(string value)
		{
			_builder.Append(value).Append(_newLine);
			return this;
		}

		public override string ToString() => _builder.ToString();
	}
}
