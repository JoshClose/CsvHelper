// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The size of the buffer used for parsing and writing CSV files.
	/// Default is 0x1000.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class BufferSizeAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// The buffer size.
		/// </summary>
		public int BufferSize { get; private set; }

		/// <summary>
		/// The size of the buffer used for parsing and writing CSV files.
		/// </summary>
		/// <param name="bufferSize"></param>
		public BufferSizeAttribute(int bufferSize)
		{
			BufferSize = bufferSize;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.BufferSize = BufferSize;
		}
	}
}
