// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// A value indicating whether changes in the column
	/// count should be detected. If <see langword="true"/>, a <see cref="BadDataException"/>
	/// will be thrown if a different column count is detected.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class DetectColumnCountChangesAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// A value indicating whether changes in the column
		/// count should be detected. If <see langword="true"/>, a <see cref="BadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		public bool DetectColumnCountChanges { get; private set; }

		/// <summary>
		/// A value indicating whether changes in the column
		/// count should be detected. If <see langword="true"/>, a <see cref="BadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		public DetectColumnCountChangesAttribute(bool detectColumnCountChanges = true)
		{
			DetectColumnCountChanges = detectColumnCountChanges;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.DetectColumnCountChanges = DetectColumnCountChanges;
		}
	}
}
