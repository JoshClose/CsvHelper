// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The delimiter used to separate fields.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class DelimiterAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets the delimiter.
		/// </summary>
		public string Delimiter { get; private set; }

		/// <summary>
		/// The delimiter used to separate fields.
		/// </summary>
		/// <param name="delimiter">The delimiter.</param>
		public DelimiterAttribute(string delimiter)
		{
			Delimiter = delimiter;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.Delimiter = Delimiter;
		}
	}
}
