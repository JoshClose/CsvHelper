// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Detect the delimiter instead of using the delimiter from configuration.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class DetectDelimiterAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Detect the delimiter instead of using the delimiter from configuration.
		/// </summary>
		public bool DetectDelimiter { get; private set; }

		/// <summary>
		/// Detect the delimiter instead of using the delimiter from configuration.
		/// </summary>
		public DetectDelimiterAttribute(bool detectDelimiter = true)
		{
			DetectDelimiter = detectDelimiter;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.DetectDelimiter = DetectDelimiter;
		}
	}
}
