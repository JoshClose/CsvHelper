// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Text;

namespace CsvHelper.Tests
{
	/// <summary>
	/// A <see cref="StringBuilder"/> like class with configurable line ending for unit tests.
	/// </summary>
	public class TestStringBuilder
	{
		private readonly string newLine;
		private readonly StringBuilder builder;

		public TestStringBuilder(string newLine)
		{
			this.newLine = newLine ?? throw new ArgumentNullException(nameof(newLine));
			builder = new StringBuilder();
		}

		public TestStringBuilder AppendLine(string value)
		{
			builder.Append(value).Append(newLine);
			return this;
		}

		public override string ToString() => builder.ToString();

		public static implicit operator string(TestStringBuilder sb) => sb.ToString();
	}
}
