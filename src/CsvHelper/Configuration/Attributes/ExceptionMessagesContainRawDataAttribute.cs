// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// A value indicating whether exception messages contain raw CSV data.
	/// <see langword="true"/> if exceptions contain raw CSV data, otherwise <see langword="false"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class ExceptionMessagesContainRawDataAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// A value indicating whether exception messages contain raw CSV data.
		/// <see langword="true"/> if exceptions contain raw CSV data, otherwise <see langword="false"/>.
		/// </summary>
		public bool ExceptionMessagesContainRawData { get; private set; }

		/// <summary>
		/// A value indicating whether exception messages contain raw CSV data.
		/// <see langword="true"/> if exceptions contain raw CSV data, otherwise <see langword="false"/>.
		/// </summary>
		public ExceptionMessagesContainRawDataAttribute(bool exceptionMessagesContainRawData = true)
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
