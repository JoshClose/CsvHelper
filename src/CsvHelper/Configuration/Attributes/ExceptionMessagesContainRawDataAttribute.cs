// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// A value indicating if exception messages contain raw CSV data.
	/// <c>true</c> if exception contain raw CSV data, otherwise <c>false</c>.
	/// Default is <c>true</c>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class ExceptionMessagesContainRawDataAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// A value indicating if exception messages contain raw CSV data.
		/// <c>true</c> if exception contain raw CSV data, otherwise <c>false</c>.
		/// </summary>
		public bool ExceptionMessagesContainRawData { get; private set; }

		/// <summary>
		/// A value indicating if exception messages contain raw CSV data.
		/// <c>true</c> if exception contain raw CSV data, otherwise <c>false</c>.
		/// </summary>
		/// <param name="exceptionMessagesContainRawData"></param>
		public ExceptionMessagesContainRawDataAttribute(bool exceptionMessagesContainRawData)
		{
			ExceptionMessagesContainRawData = exceptionMessagesContainRawData;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.ExceptionMessagesContainRawData = ExceptionMessagesContainRawData;
		}
	}
}
